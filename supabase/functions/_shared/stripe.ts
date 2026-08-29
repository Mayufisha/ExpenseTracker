import Stripe from 'npm:stripe@^22'

export type PaymentMode = 'test'

export function createStripeClient(): { stripe: Stripe; mode: PaymentMode } {
  const mode = Deno.env.get('PAYMENTS_MODE')
  if (mode !== 'test') {
    throw Object.assign(new Error(
      'The Stripe adapter is sandbox-only because Stripe prohibits personal peer-to-peer money transmission.',
    ), { status: 503 })
  }

  const secretKey = Deno.env.get('STRIPE_SECRET_KEY')
  if (!secretKey) throw new Error('STRIPE_SECRET_KEY is not configured.')
  if (!secretKey.startsWith('sk_test_')) {
    throw new Error('Test mode requires a Stripe test secret key.')
  }

  return { stripe: new Stripe(secretKey), mode }
}
