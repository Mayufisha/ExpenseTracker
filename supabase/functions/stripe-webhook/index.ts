import Stripe from 'npm:stripe@^22'
import { createAdminClient } from '../_shared/supabase.ts'
import { errorResponse, jsonResponse } from '../_shared/http.ts'
import { createStripeClient } from '../_shared/stripe.ts'

const cryptoProvider = Stripe.createSubtleCryptoProvider()

Deno.serve(async (req) => {
  if (req.method !== 'POST') return jsonResponse({ message: 'Method not allowed.' }, 405)

  try {
    const { stripe } = createStripeClient()
    const signingSecret = Deno.env.get('STRIPE_WEBHOOK_SIGNING_SECRET')
    if (!signingSecret) throw new Error('STRIPE_WEBHOOK_SIGNING_SECRET is not configured.')

    const signature = req.headers.get('Stripe-Signature')
    if (!signature) return jsonResponse({ message: 'Missing Stripe signature.' }, 400)
    const payload = await req.text()
    const event = await stripe.webhooks.constructEventAsync(
      payload,
      signature,
      signingSecret,
      undefined,
      cryptoProvider,
    )

    const admin = createAdminClient()
    const requestId = getPaymentRequestId(event)
    const { error: eventError } = await admin.from('payment_events').insert({
      provider: 'stripe',
      provider_event_id: event.id,
      event_type: event.type,
      payment_request_id: requestId,
      payload: event,
    })
    if (eventError?.code === '23505') return jsonResponse({ received: true, duplicate: true })
    if (eventError) throw eventError

    if (requestId) {
      const update = getStatusUpdate(event)
      if (update) {
        const { error } = await admin.from('payment_requests').update({
          ...update,
          updated_at: new Date().toISOString(),
        }).eq('id', requestId)
        if (error) throw error
      }
    }

    if (event.type === 'account.updated') {
      const account = event.data.object as Stripe.Account
      await admin.from('payment_accounts').update({
        charges_enabled: account.charges_enabled ?? false,
        payouts_enabled: account.payouts_enabled ?? false,
        details_submitted: account.details_submitted ?? false,
        updated_at: new Date().toISOString(),
      }).eq('provider_account_id', account.id)
    }

    return jsonResponse({ received: true })
  } catch (error) {
    return errorResponse(error, 400)
  }
})

function getPaymentRequestId(event: Stripe.Event): string | null {
  const object = event.data.object as unknown as { metadata?: Record<string, string> }
  return object.metadata?.payment_request_id ?? null
}

function getStatusUpdate(event: Stripe.Event): Record<string, unknown> | null {
  switch (event.type) {
    case 'checkout.session.completed': {
      const session = event.data.object as Stripe.Checkout.Session
      return session.payment_status === 'paid'
        ? { status: 'paid', paid_at: new Date().toISOString(), failure_message: null }
        : { status: 'processing' }
    }
    case 'checkout.session.async_payment_succeeded':
    case 'payment_intent.succeeded':
      return { status: 'paid', paid_at: new Date().toISOString(), failure_message: null }
    case 'checkout.session.async_payment_failed':
    case 'payment_intent.payment_failed':
      return { status: 'failed', failure_message: 'The card payment failed.' }
    case 'checkout.session.expired':
      return { status: 'expired' }
    case 'charge.refunded':
      return { status: 'refunded' }
    default:
      return null
  }
}
