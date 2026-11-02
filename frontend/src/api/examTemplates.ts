import http from './http'
import type { AvailableExam, ExamTemplate, SaveExamTemplateRequest } from './types'

export const examTemplatesApi = {
  list: () => http.get<ExamTemplate[]>('/exam-templates').then((r) => r.data),
  available: () => http.get<AvailableExam[]>('/exam-templates/available').then((r) => r.data),
  create: (body: SaveExamTemplateRequest) => http.post<ExamTemplate>('/exam-templates', body).then((r) => r.data),
  update: (id: number, body: SaveExamTemplateRequest) =>
    http.put<ExamTemplate>(`/exam-templates/${id}`, body).then((r) => r.data),
  remove: (id: number) => http.delete(`/exam-templates/${id}`).then(() => undefined),
}
