import { corsHeaders, errorResponse, jsonResponse, withQueryValue } from '../_shared/http.ts'
import { createAdminClient, requireUser } from '../_shared/supabase.ts'
import { createStripeClient } from '../_shared/stripe.ts'

type CardRequest = {
  splitSyncId: string
  participantSyncId: string
  participantName: string
  amount: number
  currency: string
  idempotencyKey: string
  description: string
}

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

Deno.serve(async (req) => {
  if (req.method === 'OPTIONS') return new Response('ok', { headers: corsHeaders })
  if (req.method !== 'POST') return jsonResponse({ message: 'Method not allowed.' }, 405)

  try {
    const user = await requireUser(req)
    const input = await req.json() as CardRequest
    validate(input)

    const admin = createAdminClient()
    const { stripe, mode } = createStripeClient()
    const { data: existing } = await admin
      .from('payment_requests')
      .select('id, checkout_url, status, expires_at')
      .eq('owner_id', user.id)
      .eq('idempotency_key', input.idempotencyKey)
      .maybeSingle()
    if (existing?.checkout_url) {
      return jsonResponse({
        id: existing.id,
        checkoutUrl: existing.checkout_url,
        status: existing.status,
        expiresAt: existing.expires_at,
        mode,
      })
    }

    const { data: paymentAccount, error: accountError } = await admin
      .from('payment_accounts')
      .select('*')
      .eq('user_id', user.id)
      .maybeSingle()
    if (accountError) throw accountError
    if (!paymentAccount) {
      throw Object.assign(new Error('Set up card collection in Settings first.'), { status: 409 })
    }
    if (paymentAccount.test_mode !== (mode === 'test')) {
      throw Object.assign(new Error(`The recipient account is not configured for ${mode} mode.`), { status: 409 })
    }

    const connectedAccountResult = await stripe.accounts.retrieve(paymentAccount.provider_account_id)
    if ('deleted' in connectedAccountResult && connectedAccountResult.deleted) {
      throw Object.assign(new Error('The Stripe test recipient account was deleted.'), { status: 409 })
    }
    const connectedAccount = connectedAccountResult
    const chargesEnabled = connectedAccount.charges_enabled ?? false
    const payoutsEnabled = connectedAccount.payouts_enabled ?? false
    await admin.from('payment_accounts').update({
      charges_enabled: chargesEnabled,
      payouts_enabled: payoutsEnabled,
      details_submitted: connectedAccount.details_submitted ?? false,
      updated_at: new Date().toISOString(),
    }).eq('user_id', user.id)
    if (!chargesEnabled || !payoutsEnabled) {
      throw Object.assign(new Error('Finish Stripe recipient onboarding before requesting card payment.'), { status: 409 })
    }

    const requestId = existing?.id ?? crypto.randomUUID()
    const expiresAt = new Date(Date.now() + 23 * 60 * 60 * 1000)
    if (!existing) {
      const { error: insertError } = await admin.from('payment_requests').insert({
        id: requestId,
        owner_id: user.id,
        split_sync_id: input.splitSyncId,
        participant_sync_id: input.participantSyncId,
        participant_name: input.participantName.trim(),
        method: 'card',
        amount: input.amount,
        currency: input.currency.toLowerCase(),
        provider: 'stripe-test',
        idempotency_key: input.idempotencyKey,
        expires_at: expiresAt.toISOString(),
      })
      if (insertError) throw insertError
    }

    try {
      const successUrl = Deno.env.get('PAYMENT_SUCCESS_URL')
      const cancelUrl = Deno.env.get('PAYMENT_CANCEL_URL')
      if (!successUrl || !cancelUrl) throw new Error('Payment return URLs are not configured.')

      const session = await stripe.checkout.sessions.create({
        mode: 'payment',
        payment_method_types: ['card'],
        client_reference_id: requestId,
        line_items: [{
          quantity: 1,
          price_data: {
            currency: input.currency.toLowerCase(),
            unit_amount: Math.round(input.amount * 100),
            product_data: { name: input.description.trim().slice(0, 120) },
          },
        }],
        metadata: {
          payment_request_id: requestId,
          split_sync_id: input.splitSyncId,
          participant_sync_id: input.participantSyncId,
        },
        payment_intent_data: {
          transfer_data: { destination: connectedAccount.id },
          metadata: { payment_request_id: requestId },
        },
        success_url: withQueryValue(successUrl, 'payment_request_id', requestId),
        cancel_url: withQueryValue(cancelUrl, 'payment_request_id', requestId),
        expires_at: Math.floor(expiresAt.getTime() / 1000),
      }, { idempotencyKey: `checkout-${input.idempotencyKey}` })
      if (!session.url) throw new Error('Stripe did not return a Checkout URL.')

      const { error: updateError } = await admin.from('payment_requests').update({
        provider_session_id: session.id,
        checkout_url: session.url,
        status: 'pending',
        updated_at: new Date().toISOString(),
      }).eq('id', requestId)
      if (updateError) throw updateError

      return jsonResponse({
        id: requestId,
        checkoutUrl: session.url,
        status: 'pending',
        expiresAt: expiresAt.toISOString(),
        mode,
      })
    } catch (error) {
      await admin.from('payment_requests').update({
        status: 'failed',
        failure_message: error instanceof Error ? error.message : 'Checkout creation failed.',
        updated_at: new Date().toISOString(),
      }).eq('id', requestId)
      throw error
    }
  } catch (error) {
    return errorResponse(error)
  }
})

function validate(input: CardRequest): void {
  if (!input.splitSyncId?.trim() || !input.participantSyncId?.trim())
    throw Object.assign(new Error('Split and participant identifiers are required.'), { status: 400 })
  if (!input.participantName?.trim())
    throw Object.assign(new Error('Participant name is required.'), { status: 400 })
  if (!Number.isFinite(input.amount) || input.amount <= 0 || input.amount > 10000)
    throw Object.assign(new Error('Payment amount must be between 0.01 and 10,000.'), { status: 400 })
  if (!/^[a-z]{3}$/i.test(input.currency))
    throw Object.assign(new Error('A valid three-letter currency is required.'), { status: 400 })
  if (!uuidPattern.test(input.idempotencyKey))
    throw Object.assign(new Error('A valid idempotency key is required.'), { status: 400 })
  if (!input.description?.trim())
    throw Object.assign(new Error('Payment description is required.'), { status: 400 })
}
