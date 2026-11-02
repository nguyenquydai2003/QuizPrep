import http from './http'
import type {
  AiStatus,
  ImportCommitResult,
  ImportItem,
  ImportPreview,
  QuestionType,
  Role,
  StatsOverview,
  UserAdmin,
} from './types'

export const importApi = {
  preview: (file: File, defaultTopicId?: number) => {
    const form = new FormData()
    form.append('file', file)
    if (defaultTopicId) form.append('defaultTopicId', String(defaultTopicId))
    return http.post<ImportPreview>('/import/preview', form).then((r) => r.data)
  },
  commit: (items: ImportItem[], defaultTopicId?: number) =>
    http.post<ImportCommitResult>('/import/commit', { items, defaultTopicId }).then((r) => r.data),
  /** Tải file mẫu (cần token nên không dùng thẻ <a href> trực tiếp). */
  downloadTemplate: async (kind: 'excel' | 'word') => {
    const res = await http.get<Blob>(`/import/templates/${kind}`, { responseType: 'blob' })
    const url = URL.createObjectURL(res.data)
    const link = document.createElement('a')
    link.href = url
    link.download = kind === 'excel' ? 'mau-cau-hoi.xlsx' : 'mau-cau-hoi.docx'
    link.click()
    URL.revokeObjectURL(url)
  },
}

export const usersApi = {
  list: () => http.get<UserAdmin[]>('/users').then((r) => r.data),
  create: (body: { userName: string; displayName: string; password: string; role: Role }) =>
    http.post<UserAdmin>('/users', body).then((r) => r.data),
  update: (id: string, body: { displayName: string; role: Role }) => http.put(`/users/${id}`, body).then(() => undefined),
  resetPassword: (id: string, newPassword: string) =>
    http.post(`/users/${id}/reset-password`, { newPassword }).then(() => undefined),
  remove: (id: string) => http.delete(`/users/${id}`).then(() => undefined),
}

export const statsApi = {
  overview: (userId?: string) =>
    http
      .get<StatsOverview>('/stats/overview', {
        // getTimezoneOffset() trả về số phút *trừ* đi (UTC+7 => -420)
        params: { userId, tzOffsetMinutes: -new Date().getTimezoneOffset() },
      })
      .then((r) => r.data),
}

export const aiApi = {
  status: () => http.get<AiStatus>('/ai/status').then((r) => r.data),
  explain: (body: { type: QuestionType; content: string; options: { content: string; isCorrect: boolean }[] }) =>
    http.post<{ explanation: string }>('/ai/explain', body).then((r) => r.data.explanation),
}
