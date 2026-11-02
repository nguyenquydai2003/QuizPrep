<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { onBeforeRouteLeave, useRouter } from 'vue-router'
import axios from 'axios'
import { ElMessage, ElMessageBox } from 'element-plus'
import { AlarmClock, ArrowLeft, ArrowRight, Close, Flag, Grid } from '@element-plus/icons-vue'
import { attemptsApi } from '@/api/attempts'
import type { Attempt, AttemptQuestion } from '@/api/types'
import QuestionCard from '@/components/QuestionCard.vue'
import { getErrorMessage } from '@/utils/errors'
import { formatClock, modeLabels } from '@/utils/labels'

const props = defineProps<{ id: string }>()
const router = useRouter()

const attempt = ref<Attempt | null>(null)
const current = ref(0)
const loading = ref(true)
const checking = ref(false)
const submitting = ref(false)
const showNav = ref(false)

/** Lựa chọn chưa gửi lên server (luyện tập: trước khi bấm Kiểm tra). */
const drafts = reactive<Record<number, number[]>>({})

const isExam = computed(() => attempt.value?.mode === 'Exam')
const question = computed<AttemptQuestion | undefined>(() => attempt.value?.questions[current.value])
const total = computed(() => attempt.value?.questions.length ?? 0)

const selectedOf = (q: AttemptQuestion) => drafts[q.id] ?? q.selectedKeys
const isLocked = (q: AttemptQuestion) => attempt.value?.status === 'Submitted' || (!isExam.value && q.revealed)
const isAnswered = (q: AttemptQuestion) => (isExam.value ? selectedOf(q).length > 0 : q.revealed)
const answeredCount = computed(() => attempt.value?.questions.filter(isAnswered).length ?? 0)
const allRevealed = computed(() => !isExam.value && answeredCount.value === total.value)

// ---------- Đồng hồ (theo giờ server, F5 không reset) ----------
let clockOffset = 0
const now = ref(Date.now())
let tick: number | undefined
const remainingSeconds = computed(() => {
  const deadline = attempt.value?.deadline
  return deadline ? (new Date(deadline).getTime() - (now.value + clockOffset)) / 1000 : null
})
const timerLevel = computed(() => {
  const r = remainingSeconds.value
  if (r == null) return ''
  return r <= 60 ? 'critical' : r <= 300 ? 'warning' : ''
})
let autoSubmitted = false
watch(remainingSeconds, (r) => {
  if (r != null && r <= 0 && attempt.value?.status === 'InProgress' && !autoSubmitted) {
    autoSubmitted = true
    autoSubmit()
  }
})

// ---------- Thời gian làm từng câu ----------
const timeSpent: Record<number, number> = {}
let shownAt = Date.now()
function recordTime() {
  const q = question.value
  if (q) timeSpent[q.id] = (timeSpent[q.id] ?? 0) + (Date.now() - shownAt) / 1000
  shownAt = Date.now()
}
function takeTime(questionId: number) {
  if (question.value?.id === questionId) recordTime()
  const seconds = Math.round(timeSpent[questionId] ?? 0)
  timeSpent[questionId] = 0
  return seconds
}

// ---------- Tải bài ----------
async function load() {
  try {
    const data = await attemptsApi.get(props.id)
    if (data.status === 'Submitted') {
      router.replace({ name: 'attempt-result', params: { id: data.id } })
      return
    }
    clockOffset = new Date(data.serverNow).getTime() - Date.now()
    attempt.value = data
    const firstOpen = data.questions.findIndex((q) => (data.mode === 'Exam' ? q.selectedKeys.length === 0 : !q.revealed))
    current.value = Math.max(0, firstOpen)
    shownAt = Date.now()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
    router.replace('/')
  } finally {
    loading.value = false
  }
}

// ---------- Thi thử: tự lưu ----------
const saveTimers = new Map<number, number>()
const pendingSaves = reactive(new Set<number>())
const savingCount = ref(0)
const saveFailed = ref(false)

function scheduleSave(q: AttemptQuestion) {
  window.clearTimeout(saveTimers.get(q.id))
  saveTimers.set(q.id, window.setTimeout(() => save(q), 400))
  pendingSaves.add(q.id)
}

async function save(q: AttemptQuestion) {
  saveTimers.delete(q.id)
  pendingSaves.delete(q.id)
  const keys = drafts[q.id]
  if (!keys || !attempt.value) return
  savingCount.value++
  try {
    const res = await attemptsApi.answer(attempt.value.id, q.id, keys, takeTime(q.id))
    q.selectedKeys = res.selectedKeys
    if (drafts[q.id] === keys) delete drafts[q.id]
    saveFailed.value = false
  } catch (e) {
    if (axios.isAxiosError(e) && e.response?.status === 409) {
      ElMessage.warning(getErrorMessage(e))
      router.replace({ name: 'attempt-result', params: { id: props.id } })
      return
    }
    saveFailed.value = true
    ElMessage.error(getErrorMessage(e, 'Không lưu được đáp án, hãy thử chọn lại.'))
  } finally {
    savingCount.value--
  }
}

async function flushSaves() {
  const pending = [...saveTimers.keys()]
  pending.forEach((id) => window.clearTimeout(saveTimers.get(id)))
  await Promise.all(
    pending.map((id) => {
      const q = attempt.value?.questions.find((x) => x.id === id)
      return q ? save(q) : Promise.resolve()
    }),
  )
}

const saveStatus = computed(() => {
  if (savingCount.value > 0 || pendingSaves.size > 0) return 'Đang lưu…'
  return saveFailed.value ? 'Lưu thất bại' : 'Đã lưu'
})

// ---------- Thao tác ----------
function toggle(key: number) {
  const q = question.value
  if (!q || isLocked(q)) return
  const current = selectedOf(q)
  const next =
    q.type === 'SingleChoice'
      ? current[0] === key
        ? []
        : [key]
      : current.includes(key)
        ? current.filter((k) => k !== key)
        : [...current, key]
  drafts[q.id] = next
  if (isExam.value) scheduleSave(q)
}

async function check() {
  const q = question.value
  if (!q || !attempt.value || q.revealed) return
  const keys = selectedOf(q)
  if (keys.length === 0) {
    ElMessage.warning('Hãy chọn đáp án trước.')
    return
  }
  checking.value = true
  try {
    const res = await attemptsApi.answer(attempt.value.id, q.id, keys, takeTime(q.id))
    attempt.value.questions[current.value] = res
    delete drafts[q.id]
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    checking.value = false
  }
}

function go(index: number) {
  if (index < 0 || index >= total.value || index === current.value) return
  recordTime()
  current.value = index
  showNav.value = false
  window.scrollTo({ top: 0 })
}

async function toggleFlag() {
  const q = question.value
  if (!q || !attempt.value) return
  try {
    const res = await attemptsApi.flag(attempt.value.id, q.id, !q.flagged)
    q.flagged = res.flagged
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
}

async function submit() {
  if (!attempt.value) return
  await flushSaves()
  const unanswered = total.value - answeredCount.value
  const flagged = attempt.value.questions.filter((q) => q.flagged).length
  const message = isExam.value
    ? [
        unanswered ? `Còn ${unanswered} câu chưa trả lời (tính là sai).` : 'Bạn đã trả lời tất cả các câu.',
        flagged ? `${flagged} câu đang được đánh dấu xem lại.` : '',
        'Nộp bài?',
      ]
        .filter(Boolean)
        .join(' ')
    : unanswered
      ? `Còn ${unanswered} câu chưa làm, các câu này sẽ không tính điểm. Kết thúc?`
      : 'Kết thúc và xem kết quả?'
  try {
    await ElMessageBox.confirm(message, isExam.value ? 'Nộp bài' : 'Kết thúc', {
      confirmButtonText: isExam.value ? 'Nộp bài' : 'Kết thúc',
      cancelButtonText: 'Làm tiếp',
      type: unanswered ? 'warning' : 'info',
    })
  } catch {
    return
  }
  await finish()
}

async function autoSubmit() {
  ElMessage.warning('Hết giờ! Bài làm đã được nộp tự động.')
  await flushSaves()
  await finish()
}

async function finish() {
  if (!attempt.value || submitting.value) return
  submitting.value = true
  try {
    await attemptsApi.submit(attempt.value.id)
    router.replace({ name: 'attempt-result', params: { id: attempt.value.id } })
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
    submitting.value = false
  }
}

// ---------- Phím tắt ----------
function onKeydown(ev: KeyboardEvent) {
  const target = ev.target as HTMLElement
  if (ev.ctrlKey || ev.metaKey || ev.altKey) return
  if (target.closest('input, textarea, .el-message-box, .el-drawer') || document.querySelector('.el-message-box')) return
  const q = question.value
  if (!q) return

  const key = ev.key.toLowerCase()
  const optionIndex = key >= 'a' && key <= 'j' ? key.charCodeAt(0) - 97 : key >= '1' && key <= '9' ? Number(key) - 1 : -1
  if (key === 'arrowright') go(current.value + 1)
  else if (key === 'arrowleft') go(current.value - 1)
  else if (key === 'f' && isExam.value) toggleFlag()
  else if (key === 'enter') {
    if (!isExam.value && !q.revealed) check()
    else go(current.value + 1)
  } else if (optionIndex >= 0 && optionIndex < q.options.length) toggle(q.options[optionIndex]!.key)
  else return
  ev.preventDefault()
}

onMounted(() => {
  load()
  tick = window.setInterval(() => (now.value = Date.now()), 1000)
  window.addEventListener('keydown', onKeydown)
})
onBeforeUnmount(() => {
  window.clearInterval(tick)
  window.removeEventListener('keydown', onKeydown)
})
onBeforeRouteLeave(() => flushSaves())

function navClass(q: AttemptQuestion, index: number) {
  return {
    current: index === current.value,
    answered: isAnswered(q) && (isExam.value || q.isCorrect == null),
    correct: !isExam.value && q.revealed && q.isCorrect === true,
    wrong: !isExam.value && q.revealed && q.isCorrect === false,
    flagged: q.flagged,
  }
}
</script>

<template>
  <div v-loading="loading" class="attempt-page">
    <template v-if="attempt && question">
      <header class="bar">
        <el-button text :icon="Close" aria-label="Thoát" @click="router.push('/')">
          <span class="hide-sm">Thoát</span>
        </el-button>
        <div class="title">
          <strong>{{ attempt.title }}</strong>
          <span class="muted small">{{ modeLabels[attempt.mode] }} · {{ answeredCount }}/{{ total }} câu</span>
        </div>
        <div class="spacer" />
        <span v-if="isExam" class="save-status muted small hide-sm">{{ saveStatus }}</span>
        <div v-if="remainingSeconds != null" class="timer" :class="timerLevel" role="timer" aria-live="off">
          <el-icon><AlarmClock /></el-icon>{{ formatClock(remainingSeconds) }}
        </div>
        <el-button class="show-sm" :icon="Grid" aria-label="Danh sách câu" @click="showNav = true" />
        <el-button type="primary" :loading="submitting" @click="submit">{{ isExam ? 'Nộp bài' : 'Kết thúc' }}</el-button>
      </header>
      <el-progress :percentage="total ? Math.round((answeredCount / total) * 100) : 0" :show-text="false" :stroke-width="3" class="progress" />

      <div class="layout">
        <main>
          <el-card shadow="never">
            <QuestionCard :question="question" :selected="selectedOf(question)" :locked="isLocked(question)" :total="total" @toggle="toggle">
              <template v-if="isExam" #actions>
                <el-button size="small" :type="question.flagged ? 'warning' : 'default'" :icon="Flag" plain @click="toggleFlag">
                  {{ question.flagged ? 'Đã đánh dấu' : 'Đánh dấu' }}
                </el-button>
              </template>
            </QuestionCard>

            <div class="controls">
              <el-button :icon="ArrowLeft" :disabled="current === 0" @click="go(current - 1)">Trước</el-button>
              <div class="spacer" />
              <el-button
                v-if="!isExam && !question.revealed"
                type="primary"
                :loading="checking"
                :disabled="selectedOf(question).length === 0"
                @click="check"
              >
                Kiểm tra
              </el-button>
              <el-button v-if="allRevealed && current === total - 1" type="success" :loading="submitting" @click="finish">
                Xem kết quả
              </el-button>
              <el-button v-else :disabled="current === total - 1" @click="go(current + 1)">
                Tiếp <el-icon class="el-icon--right"><ArrowRight /></el-icon>
              </el-button>
            </div>
          </el-card>
          <p class="muted shortcuts hide-sm">
            Phím tắt: <kbd>A</kbd>–<kbd>J</kbd> hoặc <kbd>1</kbd>–<kbd>9</kbd> chọn đáp án · <kbd>←</kbd> <kbd>→</kbd> chuyển câu ·
            <kbd>Enter</kbd> {{ isExam ? 'câu tiếp' : 'kiểm tra / câu tiếp' }}<template v-if="isExam"> · <kbd>F</kbd> đánh dấu</template>
          </p>
        </main>

        <aside class="nav-panel hide-sm">
          <el-card shadow="never">
            <div class="nav-grid">
              <button
                v-for="(q, i) in attempt.questions"
                :key="q.id"
                type="button"
                class="nav-item"
                :class="navClass(q, i)"
                :aria-label="`Câu ${i + 1}`"
                :aria-current="i === current ? 'step' : undefined"
                @click="go(i)"
              >
                {{ i + 1 }}
              </button>
            </div>
            <div class="legend">
              <span><i class="dot answered" />Đã làm</span>
              <template v-if="!isExam">
                <span><i class="dot correct" />Đúng</span>
                <span><i class="dot wrong" />Sai</span>
              </template>
              <span v-else><i class="dot flagged" />Đánh dấu</span>
            </div>
          </el-card>
        </aside>
      </div>

      <el-drawer v-model="showNav" title="Danh sách câu" direction="btt" size="auto">
        <div class="nav-grid">
          <button v-for="(q, i) in attempt.questions" :key="q.id" type="button" class="nav-item" :class="navClass(q, i)" @click="go(i)">
            {{ i + 1 }}
          </button>
        </div>
      </el-drawer>
    </template>
  </div>
</template>

<style scoped>
.attempt-page {
  min-height: 100%;
}

.bar {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  gap: 10px;
  height: 56px;
  padding: 0 12px;
  background: var(--app-surface);
}

.title {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.title strong {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.spacer {
  flex: 1;
}

.small {
  font-size: 12px;
}

.progress {
  position: sticky;
  top: 56px;
  z-index: 10;
}

.progress :deep(.el-progress-bar__outer) {
  border-radius: 0;
}

.timer {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 10px;
  border-radius: 6px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  background: var(--el-fill-color-light);
}

.timer.warning {
  background: #fff4dc;
}

.timer.warning .el-icon {
  color: var(--status-warning);
}

.timer.critical {
  background: var(--el-color-danger-light-9);
  color: var(--status-critical);
}

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 260px;
  gap: 16px;
  max-width: 1200px;
  margin: 0 auto;
  padding: 16px;
}

.controls {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid var(--app-border);
}

.shortcuts {
  font-size: 12px;
}

kbd {
  padding: 1px 5px;
  border: 1px solid var(--app-border);
  border-bottom-width: 2px;
  border-radius: 4px;
  font-family: var(--app-mono);
  font-size: 11px;
  background: var(--app-surface);
}

.nav-panel {
  position: sticky;
  top: 76px;
  align-self: start;
}

.nav-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(36px, 1fr));
  gap: 6px;
}

.nav-item {
  position: relative;
  height: 36px;
  border: 1px solid var(--app-border);
  border-radius: 6px;
  background: var(--app-surface);
  font: inherit;
  font-size: 13px;
  cursor: pointer;
}

.nav-item:hover {
  border-color: var(--el-color-primary-light-5);
}

.nav-item.answered {
  background: var(--el-color-primary-light-8);
  border-color: var(--el-color-primary-light-5);
}

.nav-item.correct {
  background: var(--app-correct-bg);
  border-color: var(--status-good);
}

.nav-item.wrong {
  background: var(--el-color-danger-light-9);
  border-color: var(--status-critical);
}

.nav-item.current {
  outline: 2px solid var(--el-color-primary);
  outline-offset: 1px;
  font-weight: 700;
}

.nav-item.flagged::after {
  content: '';
  position: absolute;
  top: 3px;
  right: 3px;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--status-warning);
}

.legend {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 12px;
  font-size: 12px;
  color: var(--app-text-muted);
}

.legend span {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.dot {
  width: 10px;
  height: 10px;
  border-radius: 3px;
  border: 1px solid;
}

.dot.answered {
  background: var(--el-color-primary-light-8);
  border-color: var(--el-color-primary-light-5);
}

.dot.correct {
  background: var(--app-correct-bg);
  border-color: var(--status-good);
}

.dot.wrong {
  background: var(--el-color-danger-light-9);
  border-color: var(--status-critical);
}

.dot.flagged {
  border-radius: 50%;
  border: none;
  background: var(--status-warning);
}

.show-sm {
  display: none;
}

@media (max-width: 900px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
    padding: 12px;
  }

  .hide-sm {
    display: none;
  }

  .show-sm {
    display: inline-flex;
  }
}
</style>
