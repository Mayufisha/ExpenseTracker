import { corsHeaders, errorResponse, jsonResponse } from '../_shared/http.ts'
import { createAdminClient, requireUser } from '../_shared/supabase.ts'

type InteracRequest = {
  splitSyncId: string
  participantSyncId: string
  participantName: string
  amount: number
  idempotencyKey: string
}

Deno.serve(async (req) => {
  if (req.method === 'OPTIONS') return new Response('ok', { headers: corsHeaders })
  if (req.method !== 'POST') return jsonResponse({ message: 'Method not allowed.' }, 405)

  try {
    const user = await requireUser(req)
    const input = await req.json() as InteracRequest
    if (!input.splitSyncId?.trim() || !input.participantSyncId?.trim() || !input.participantName?.trim())
      throw Object.assign(new Error('Split and participant details are required.'), { status: 400 })
    if (!Number.isFinite(input.amount) || input.amount <= 0 || input.amount > 10000)
      throw Object.assign(new Error('Request amount must be between 0.01 and 10,000.'), { status: 400 })

    const admin = createAdminClient()
    const { data: existing, error: readError } = await admin
      .from('payment_requests')
      .select('id, status, created_at')
      .eq('owner_id', user.id)
      .eq('idempotency_key', input.idempotencyKey)
      .maybeSingle()
    if (readError) throw readError
    if (existing) return jsonResponse(existing)

    const requestId = crypto.randomUUID()
    const { data, error } = await admin.from('payment_requests').insert({
      id: requestId,
      owner_id: user.id,
      split_sync_id: input.splitSyncId,
      participant_sync_id: input.participantSyncId,
      participant_name: input.participantName.trim(),
      method: 'interac',
      amount: input.amount,
      currency: 'cad',
      status: 'pending',
      provider: 'interac-bank-handoff',
      idempotency_key: input.idempotencyKey,
    })
      .select('id, status, created_at')
      .single()
    if (error) throw error

    return jsonResponse({
      id: data.id,
      status: data.status,
      message: 'Open your participating bank app and create an Interac e-Transfer Request Money request.',
    })
  } catch (error) {
    return errorResponse(error)
  }
})
