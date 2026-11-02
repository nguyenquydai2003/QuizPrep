import type { AttemptMode, Difficulty, QuestionType, Subject } from '@/api/types'

export const modeLabels: Record<AttemptMode, string> = {
  Practice: 'Luyện tập',
  Exam: 'Thi thử',
  Review: 'Ôn câu sai',
}

/** 75 => "1 phút 15 giây", 3700 => "1 giờ 1 phút" */
export function formatDuration(totalSeconds: number) {
  const s = Math.max(0, Math.round(totalSeconds))
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const sec = s % 60
  if (h > 0) return `${h} giờ ${m} phút`
  if (m > 0) return sec > 0 ? `${m} phút ${sec} giây` : `${m} phút`
  return `${sec} giây`
}

/** Đồng hồ đếm ngược: 3725 => "1:02:05", 125 => "02:05" */
export function formatClock(totalSeconds: number) {
  const s = Math.max(0, Math.floor(totalSeconds))
  const h = Math.floor(s / 3600)
  const mm = String(Math.floor((s % 3600) / 60)).padStart(2, '0')
  const ss = String(s % 60).padStart(2, '0')
  return h > 0 ? `${h}:${mm}:${ss}` : `${mm}:${ss}`
}

export function formatScore(score: number | null | undefined) {
  return score == null ? '—' : `${Number.isInteger(score) ? score : score.toFixed(1)}%`
}

/**
 * Options cho el-cascader Môn > Chủ đề.
 * leafOnly: chỉ chọn được chủ đề (môn chưa có chủ đề bị vô hiệu hóa).
 */
export function subjectCascaderOptions(subjects: Subject[], leafOnly = false) {
  return subjects.map((s) => ({
    value: s.id,
    label: s.name,
    children: s.topics.length
      ? s.topics.map((t) => ({ value: t.id, label: `${t.name} (${t.questionCount})` }))
      : undefined,
    disabled: leafOnly && s.topics.length === 0,
  }))
}

export const difficultyOptions = [
  { value: 'Easy', label: 'Dễ', tag: 'success' },
  { value: 'Medium', label: 'Trung bình', tag: 'warning' },
  { value: 'Hard', label: 'Khó', tag: 'danger' },
] as const satisfies readonly { value: Difficulty; label: string; tag: string }[]

export const questionTypeOptions = [
  { value: 'SingleChoice', label: 'Một đáp án' },
  { value: 'MultipleChoice', label: 'Nhiều đáp án' },
] as const satisfies readonly { value: QuestionType; label: string }[]

export function difficultyMeta(value: Difficulty) {
  return difficultyOptions.find((d) => d.value === value) ?? difficultyOptions[1]
}

export function questionTypeLabel(value: QuestionType) {
  return questionTypeOptions.find((t) => t.value === value)?.label ?? value
}

export function optionLetter(index: number) {
  return String.fromCharCode(65 + index)
}

export function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' })
}
