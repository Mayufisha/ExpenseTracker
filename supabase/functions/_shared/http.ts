export const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Headers': 'authorization, apikey, content-type',
}

export function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { ...corsHeaders, 'Content-Type': 'application/json' },
  })
}

export function errorResponse(error: unknown, fallbackStatus = 500): Response {
  const message = error instanceof Error ? error.message : 'Unexpected payment service error.'
  const status = typeof error === 'object' && error !== null && 'status' in error
    ? Number((error as { status: number }).status)
    : fallbackStatus
  return jsonResponse({ message }, status)
}

export function withQueryValue(rawUrl: string, key: string, value: string): string {
  const url = new URL(rawUrl)
  url.searchParams.set(key, value)
  return url.toString()
}
