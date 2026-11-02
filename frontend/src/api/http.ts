import axios from 'axios'
import router from '@/router'
import { useAuthStore } from '@/stores/auth'

const http = axios.create({ baseURL: '/api' })

http.interceptors.request.use((config) => {
  const auth = useAuthStore()
  if (auth.token) config.headers.Authorization = `Bearer ${auth.token}`
  return config
})

http.interceptors.response.use(
  (response) => response,
  (error) => {
    // Token hết hạn hoặc không hợp lệ: đăng xuất và quay về trang đăng nhập
    if (axios.isAxiosError(error) && error.response?.status === 401 && !error.config?.url?.includes('/auth/login')) {
      useAuthStore().logout()
      const current = router.currentRoute.value
      if (current.name !== 'login') router.replace({ name: 'login', query: { redirect: current.fullPath } })
    }
    return Promise.reject(error)
  },
)

export default http
