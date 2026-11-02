import axios from 'axios'
import type { ProblemDetails } from '@/api/types'

/** Lấy thông báo lỗi dễ đọc từ ProblemDetails của ASP.NET Core. */
export function getErrorMessage(error: unknown, fallback = 'Đã có lỗi xảy ra.'): string {
  if (axios.isAxiosError(error)) {
    if (!error.response) return 'Không kết nối được máy chủ. Backend đã chạy chưa?'
    const data = error.response.data as ProblemDetails | undefined
    const validation = data?.errors ? Object.values(data.errors).flat() : []
    if (validation.length) return validation.join(' ')
    if (data?.title) return data.title
  }
  return fallback
}
