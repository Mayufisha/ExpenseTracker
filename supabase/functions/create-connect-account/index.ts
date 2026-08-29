import { corsHeaders, errorResponse, jsonResponse } from '../_shared/http.ts'
import { createAdminClient, requireUser } from '../_shared/supabase.ts'
import { createStripeClient } from '../_shared/stripe.ts'

Deno.serve(async (req) => {
  if (req.method === 'OPTIONS') return new Response('ok', { headers: corsHeaders })
  if (req.method !== 'POST') return jsonResponse({ message: 'Method not allowed.' }, 405)

  try {
    const user = await requireUser(req)
    const admin = createAdminClient()
    const { stripe, mode } = createStripeClient()
    const { data: stored, error: readError } = await admin
      .from('payment_accounts')
      .select('*')
      .eq('user_id', user.id)
      .maybeSingle()
    if (readError) throw readError

    const accountResult = stored
      ? await stripe.accounts.retrieve(stored.provider_account_id)
      : await stripe.accounts.create({
          type: 'express',
          country: 'CA',
          email: user.email,
          capabilities: {
            card_payments: { requested: true },
            transfers: { requested: true },
          },
          metadata: { money_manager_user_id: user.id },
        }, { idempotencyKey: `money-manager-account-${user.id}-${mode}` })
    if ('deleted' in accountResult && accountResult.deleted) {
      throw Object.assign(new Error('The Stripe test account was deleted. Remove its payment_accounts row and retry.'), { status: 409 })
    }
    const account = accountResult

    const { error: saveError } = await admin.from('payment_accounts').upsert({
      user_id: user.id,
      provider: 'stripe',
      provider_account_id: account.id,
      charges_enabled: account.charges_enabled ?? false,
      payouts_enabled: account.payouts_enabled ?? false,
      details_submitted: account.details_submitted ?? false,
      test_mode: mode === 'test',
      updated_at: new Date().toISOString(),
    })
    if (saveError) throw saveError

    const refreshUrl = Deno.env.get('CONNECT_REFRESH_URL')
    const returnUrl = Deno.env.get('CONNECT_RETURN_URL')
    if (!refreshUrl || !returnUrl) throw new Error('Connect return URLs are not configured.')

    const link = await stripe.accountLinks.create({
      account: account.id,
      refresh_url: refreshUrl,
      return_url: returnUrl,
      type: 'account_onboarding',
      collection_options: { fields: 'eventually_due' },
    })

    return jsonResponse({
      onboardingUrl: link.url,
      chargesEnabled: account.charges_enabled ?? false,
      payoutsEnabled: account.payouts_enabled ?? false,
      mode,
    })
  } catch (error) {
    return errorResponse(error)
  }
})
