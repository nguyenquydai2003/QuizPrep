import http from './http'
import type { SaveSubjectRequest, SaveTopicRequest, Subject, Topic } from './types'

export const subjectsApi = {
  list: () => http.get<Subject[]>('/subjects').then((r) => r.data),
  create: (body: SaveSubjectRequest) => http.post<Subject>('/subjects', body).then((r) => r.data),
  update: (id: number, body: SaveSubjectRequest) => http.put(`/subjects/${id}`, body).then(() => undefined),
  remove: (id: number) => http.delete(`/subjects/${id}`).then(() => undefined),

  createTopic: (subjectId: number, body: SaveTopicRequest) =>
    http.post<Topic>(`/subjects/${subjectId}/topics`, body).then((r) => r.data),
  updateTopic: (id: number, body: SaveTopicRequest) => http.put(`/topics/${id}`, body).then(() => undefined),
  removeTopic: (id: number) => http.delete(`/topics/${id}`).then(() => undefined),
}
