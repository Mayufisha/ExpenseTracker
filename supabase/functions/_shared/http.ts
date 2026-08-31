const allowedOrigin = normalizeAllowedOrigin(Deno.env.get('ALLOWED_ORIGIN'))

export const corsHeaders: Record<string, string> = {
  ...(allowedOrigin ? { 'Access-Control-Allow-Origin': allowedOrigin, Vary: 'Origin' } : {}),
  'Access-Control-Allow-Headers': 'authorization, apikey, content-type',
  'Access-Control-Allow-Methods': 'POST, OPTIONS',
}

export function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      ...corsHeaders,
      'Cache-Control': 'no-store',
      'Content-Type': 'application/json',
      'X-Content-Type-Options': 'nosniff',
    },
  })
}

export function errorResponse(
  error: unknown,
  fallbackStatus = 500,
  fallbackMessage = 'The payment service could not complete the request.',
): Response {
  const hasPublicStatus = typeof error === 'object' && error !== null && 'status' in error
  const requestedStatus = hasPublicStatus ? Number((error as { status: number }).status) : fallbackStatus
  const status = Number.isInteger(requestedStatus) && requestedStatus >= 400 && requestedStatus <= 599
    ? requestedStatus
    : 500
  if (!hasPublicStatus || status >= 500) console.error(error)
  const message = hasPublicStatus && status < 500 && error instanceof Error
    ? error.message
    : fallbackMessage
  return jsonResponse({ message }, status)
}

export async function readJson<T>(req: Request, maximumBytes = 16 * 1024): Promise<T> {
  const contentType = req.headers.get('content-type')?.toLowerCase() ?? ''
  if (!contentType.startsWith('application/json'))
    throw Object.assign(new Error('Content-Type must be application/json.'), { status: 415 })

  const declaredLength = Number(req.headers.get('content-length') ?? '0')
  if (Number.isFinite(declaredLength) && declaredLength > maximumBytes)
    throw Object.assign(new Error('Request body is too large.'), { status: 413 })

  const body = await req.text()
  if (new TextEncoder().encode(body).byteLength > maximumBytes)
    throw Object.assign(new Error('Request body is too large.'), { status: 413 })

  try {
    return JSON.parse(body) as T
  } catch {
    throw Object.assign(new Error('Request body must be valid JSON.'), { status: 400 })
  }
}

export function withQueryValue(rawUrl: string, key: string, value: string): string {
  const url = new URL(rawUrl)
  url.searchParams.set(key, value)
  return url.toString()
}

export function requireHttpsUrl(rawUrl: string | undefined, name: string): string {
  if (!rawUrl) throw new Error(`${name} is not configured.`)
  const url = new URL(rawUrl)
  if (url.protocol !== 'https:' || url.username || url.password)
    throw new Error(`${name} must be a credential-free HTTPS URL.`)
  return url.toString()
}

function normalizeAllowedOrigin(rawOrigin: string | undefined): string | undefined {
  if (!rawOrigin) return undefined
  try {
    const url = new URL(rawOrigin.trim())
    if (url.protocol !== 'https:' || url.username || url.password || url.origin !== rawOrigin.trim())
      return undefined
    return url.origin
  } catch {
    return undefined
  }
}
