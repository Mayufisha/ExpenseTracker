import { createClient, SupabaseClient, User } from 'npm:@supabase/supabase-js@2'

function getKey(variable: 'SUPABASE_PUBLISHABLE_KEYS' | 'SUPABASE_SECRET_KEYS', legacy: string): string {
  const collection = Deno.env.get(variable)
  if (collection) {
    const keys = JSON.parse(collection) as Record<string, string>
    if (keys.default) return keys.default
  }

  const value = Deno.env.get(legacy)
  if (!value) throw new Error(`${variable} is not configured.`)
  return value
}

export function createAdminClient(): SupabaseClient {
  return createClient(
    Deno.env.get('SUPABASE_URL')!,
    getKey('SUPABASE_SECRET_KEYS', 'SUPABASE_SERVICE_ROLE_KEY'),
    { auth: { persistSession: false, autoRefreshToken: false } },
  )
}

export async function requireUser(req: Request): Promise<User> {
  const authorization = req.headers.get('Authorization')
  if (!authorization?.startsWith('Bearer ')) {
    throw Object.assign(new Error('Authentication is required.'), { status: 401 })
  }

  const client = createClient(
    Deno.env.get('SUPABASE_URL')!,
    getKey('SUPABASE_PUBLISHABLE_KEYS', 'SUPABASE_ANON_KEY'),
    {
      auth: { persistSession: false, autoRefreshToken: false },
      global: { headers: { Authorization: authorization } },
    },
  )
  const { data, error } = await client.auth.getUser()
  if (error || !data.user) {
    throw Object.assign(new Error('The session is invalid or expired.'), { status: 401 })
  }

  return data.user
}
