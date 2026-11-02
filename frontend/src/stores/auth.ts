import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { authApi } from '@/api/auth'
import type { LoginResponse, User } from '@/api/types'

const STORAGE_KEY = 'quizprep.auth'

interface Session {
  token: string
  expiresAt: string
  user: User
}

function loadSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as Session
    return new Date(session.expiresAt).getTime() > Date.now() ? session : null
  } catch {
    return null
  }
}

export const useAuthStore = defineStore('auth', () => {
  const session = ref<Session | null>(loadSession())

  const token = computed(() => session.value?.token ?? null)
  const user = computed(() => session.value?.user ?? null)
  const isAuthenticated = computed(() => session.value !== null)
  const isAdmin = computed(() => user.value?.role === 'Admin')

  function save(res: LoginResponse) {
    session.value = { token: res.token, expiresAt: res.expiresAt, user: res.user }
    persist()
  }

  function persist() {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session.value))
    } catch {
      // Không lưu được (chế độ ẩn danh...) thì chỉ giữ phiên trong bộ nhớ
    }
  }

  async function login(userName: string, password: string) {
    save(await authApi.login(userName, password))
  }

  async function register(body: { userName: string; displayName: string; password: string }) {
    save(await authApi.register(body))
  }

  function updateUser(user: User) {
    if (!session.value) return
    session.value = { ...session.value, user }
    persist()
  }

  function logout() {
    session.value = null
    try {
      localStorage.removeItem(STORAGE_KEY)
    } catch {
      /* bỏ qua */
    }
  }

  return { token, user, isAuthenticated, isAdmin, login, register, updateUser, logout }
})
