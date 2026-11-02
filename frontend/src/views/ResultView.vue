<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { CircleCheckFilled, CircleCloseFilled } from '@element-plus/icons-vue'
import { attemptsApi } from '@/api/attempts'
import type { Attempt, AttemptQuestion } from '@/api/types'
import QuestionCard from '@/components/QuestionCard.vue'
import { getErrorMessage } from '@/utils/errors'
import { formatDateTime, formatDuration, formatScore, modeLabels } from '@/utils/labels'

const props = defineProps<{ id: string }>()
const router = useRouter()
const attempt = ref<Attempt | null>(null)
const loading = ref(true)
const restarting = ref(false)

type Filter = 'all' | 'wrong' | 'correct' | 'skipped' | 'flagged'
const filter = ref<Filter>('all')

onMounted(async () => {
  try {
    const data = await attemptsApi.get(props.id)
    if (data.status === 'InProgress') {
      router.replace({ name: 'attempt', params: { id: data.id } })
      return
    }
    attempt.value = data
    // Mặc định mở các câu sai nếu có
    if (data.questions.some((q) => q.isCorrect === false && q.selectedKeys.length > 0)) filter.value = 'wrong'
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
})

const isSkipped = (q: AttemptQuestion) => q.selectedKeys.length === 0
const matchers: Record<Filter, (q: AttemptQuestion) => boolean> = {
  all: () => true,
  wrong: (q) => q.isCorrect === false && !isSkipped(q),
  correct: (q) => q.isCorrect === true,
  skipped: isSkipped,
  flagged: (q) => q.flagged,
}
const filterLabels: Record<Filter, string> = { all: 'Tất cả', wrong: 'Sai', correct: 'Đúng', skipped: 'Bỏ trống', flagged: 'Đánh dấu' }
const filterOptions = computed(() =>
  (Object.keys(matchers) as Filter[])
    .map((f) => ({ value: f, count: attempt.value?.questions.filter(matchers[f]).length ?? 0 }))
    .filter((f) => f.value === 'all' || f.count > 0)
    .map((f) => ({ value: f.value, label: `${filterLabels[f.value]} (${f.count})` })),
)
const visible = computed(() => attempt.value?.questions.filter(matchers[filter.value]) ?? [])

const duration = computed(() => {
  const a = attempt.value
  return a?.submittedAt ? (new Date(a.submittedAt).getTime() - new Date(a.startedAt).getTime()) / 1000 : 0
})

const byTopic = computed(() => {
  const groups = new Map<string, { topic: string; subject: string; correct: number; total: number }>()
  for (const q of attempt.value?.questions ?? []) {
    if (attempt.value?.mode !== 'Exam' && isSkipped(q)) continue
    const key = `${q.subjectName}/${q.topicName}`
    const g = groups.get(key) ?? { topic: q.topicName, subject: q.subjectName, correct: 0, total: 0 }
    g.total++
    if (q.isCorrect) g.correct++
    groups.set(key, g)
  }
  return [...groups.values()].sort((a, b) => a.correct / a.total - b.correct / b.total)
})

const wrongCount = computed(() => attempt.value?.questions.filter((q) => q.isCorrect === false && !isSkipped(q)).length ?? 0)

async function retakeExam() {
  if (!attempt.value?.examTemplateId) return
  restarting.value = true
  try {
    const next = await attemptsApi.startExam(attempt.value.examTemplateId)
    router.push({ name: 'attempt', params: { id: next.id } })
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    restarting.value = false
  }
}
</script>

<template>
  <div v-loading="loading">
    <template v-if="attempt">
      <el-card shadow="never" class="summary">
        <div class="summary-main">
          <div>
            <div class="muted">{{ modeLabels[attempt.mode] }} · {{ attempt.title }}</div>
            <div class="hero">{{ formatScore(attempt.score) }}</div>
            <div v-if="attempt.passed != null" class="status" :class="attempt.passed ? 'pass' : 'fail'">
              <el-icon><component :is="attempt.passed ? CircleCheckFilled : CircleCloseFilled" /></el-icon>
              {{ attempt.passed ? 'Đạt' : 'Chưa đạt' }} <span class="muted">(cần {{ attempt.passScore }}%)</span>
            </div>
          </div>
          <dl class="facts">
            <div><dt>Đúng</dt><dd>{{ attempt.correctCount }}/{{ attempt.mode === 'Exam' ? attempt.totalQuestions : attempt.answeredCount }}</dd></div>
            <div><dt>Đã làm</dt><dd>{{ attempt.answeredCount }}/{{ attempt.totalQuestions }}</dd></div>
            <div><dt>Thời gian</dt><dd>{{ formatDuration(duration) }}</dd></div>
            <div><dt>Nộp lúc</dt><dd>{{ attempt.submittedAt ? formatDateTime(attempt.submittedAt) : '' }}</dd></div>
          </dl>
        </div>
        <div class="buttons">
          <el-button @click="router.push('/')">Về trang chủ</el-button>
          <el-button v-if="wrongCount" @click="router.push('/review')">Ôn câu sai</el-button>
          <el-button v-if="attempt.examTemplateId" type="primary" :loading="restarting" @click="retakeExam">Thi lại đề này</el-button>
          <el-button v-else-if="attempt.mode === 'Practice'" type="primary" @click="router.push('/practice')">Luyện tiếp</el-button>
        </div>
      </el-card>

      <el-card v-if="byTopic.length > 1" shadow="never" class="section">
        <template #header>Theo chủ đề</template>
        <el-table :data="byTopic" size="small">
          <el-table-column label="Chủ đề" min-width="200">
            <template #default="{ row }">{{ row.topic }} <span class="muted small">· {{ row.subject }}</span></template>
          </el-table-column>
          <el-table-column label="Đúng" width="100" align="right">
            <template #default="{ row }"><span class="num">{{ row.correct }}/{{ row.total }}</span></template>
          </el-table-column>
          <el-table-column label="Tỷ lệ" width="90" align="right">
            <template #default="{ row }"><span class="num">{{ Math.round((row.correct / row.total) * 100) }}%</span></template>
          </el-table-column>
        </el-table>
      </el-card>

      <div class="filter-bar">
        <el-segmented v-model="filter" :options="filterOptions" />
      </div>

      <el-empty v-if="visible.length === 0" description="Không có câu nào" />
      <el-card v-for="q in visible" :key="q.id" shadow="never" class="section">
        <QuestionCard :question="q" :selected="q.selectedKeys" locked :total="attempt.totalQuestions" />
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.summary-main {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 24px;
}

.hero {
  font-size: 56px;
  font-weight: 700;
  line-height: 1.1;
  margin: 4px 0;
}

.status {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-weight: 600;
}

.status.pass {
  color: var(--status-good-text);
}

.status.pass .el-icon {
  color: var(--status-good);
}

.status.fail {
  color: var(--status-critical);
}

.facts {
  display: grid;
  grid-template-columns: repeat(2, minmax(120px, auto));
  gap: 12px 32px;
  margin: 0;
}

dt {
  font-size: 13px;
  color: var(--app-text-muted);
}

dd {
  margin: 2px 0 0;
  font-size: 18px;
  font-weight: 600;
}

.buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 20px;
}

.buttons .el-button + .el-button {
  margin-left: 0;
}

.section,
.summary {
  margin-bottom: 16px;
}

.filter-bar {
  margin-bottom: 12px;
  overflow-x: auto;
}

.small {
  font-size: 12px;
}

.num {
  font-variant-numeric: tabular-nums;
}
</style>
