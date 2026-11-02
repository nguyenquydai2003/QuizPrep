import http from './http'
import type { PagedResult, QuestionDetail, QuestionListItem, QuestionQuery, SaveQuestionRequest } from './types'

export const questionsApi = {
  search: (query: QuestionQuery) =>
    http.get<PagedResult<QuestionListItem>>('/questions', { params: query }).then((r) => r.data),
  tags: () => http.get<string[]>('/questions/tags').then((r) => r.data),
  get: (id: number) => http.get<QuestionDetail>(`/questions/${id}`).then((r) => r.data),
  create: (body: SaveQuestionRequest) => http.post<QuestionDetail>('/questions', body).then((r) => r.data),
  update: (id: number, body: SaveQuestionRequest) =>
    http.put<QuestionDetail>(`/questions/${id}`, body).then((r) => r.data),
  remove: (id: number) => http.delete(`/questions/${id}`).then(() => undefined),
}
