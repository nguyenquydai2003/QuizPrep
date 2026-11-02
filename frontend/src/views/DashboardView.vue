<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Notebook, RefreshLeft, Timer } from '@element-plus/icons-vue'
import { statsApi } from '@/api/admin'
import { attemptsApi } from '@/api/attempts'
import type { StatsOverview } from '@/api/types'
import StatsDashboard from '@/components/StatsDashboard.vue'
import { getErrorMessage } from '@/utils/errors'
import { formatClock, formatDateTime, formatScore, modeLabels } from '@/utils/labels'

const router = useRouter()
const stats = ref<StatsOverview | null>(null)
const loading = ref(true)
const starting = ref(false)

onMounted(async () => {
  try {
    stats.value = await statsApi.overview()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
})

async function practiceTopic(topicId: number) {
  if (starting.value) return
  starting.value = true
  try {
    const attempt = await attemptsApi.startPractice({ topicId, count: 10, onlyUnseen: false })
    router.push({ name: 'attempt', params: { id: attempt.id } })
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    starting.value = false
  }
}

const remaining = (deadline: string | null) =>
  deadline ? formatClock((new Date(deadline).getTime() - Date.now()) / 1000) : null
</script>

<template>
  <div v-loading="loading">
    <div class="page-header">
      <h1>Xin chào, {{ stats?.displayName ?? '' }} 👋</h1>
    </div>

    <div class="actions">
      <el-card shadow="hover" class="action" @click="router.push('/practice')">
        <el-icon :size="24"><Notebook /></el-icon>
        <div>
          <strong>Luyện tập</strong>
          <div class="muted small">Theo chủ đề, xem đáp án ngay</div>
        </div>
      </el-card>
      <el-card shadow="hover" class="action" @click="router.push('/exams')">
        <el-icon :size="24"><Timer /></el-icon>
        <div>
          <strong>Thi thử</strong>
          <div class="muted small">Đếm giờ, chấm điểm khi nộp</div>
        </div>
      </el-card>
      <el-card shadow="hover" class="action" @click="router.push('/review')">
        <el-icon :size="24"><RefreshLeft /></el-icon>
        <div>
          <strong>Ôn câu sai</strong>
          <div class="muted small">
            <template v-if="stats?.dueReviewCount">{{ stats.dueReviewCount }} câu đến hạn ôn</template>
            <template v-else>Chưa có câu đến hạn</template>
          </div>
        </div>
      </el-card>
    </div>

    <el-card v-if="stats?.inProgress.length" shadow="never" class="section">
      <template #header>Bài đang làm dở</template>
      <div v-for="a in stats.inProgress" :key="a.id" class="in-progress">
        <div class="ip-info">
          <el-tag size="small" effect="plain">{{ modeLabels[a.mode] }}</el-tag>
          <strong>{{ a.title }}</strong>
          <span class="muted small">{{ a.answeredCount }}/{{ a.totalQuestions }} câu · bắt đầu {{ formatDateTime(a.startedAt) }}</span>
          <span v-if="a.deadline" class="muted small">· còn {{ remaining(a.deadline) }}</span>
        </div>
        <el-button type="primary" size="small" @click="router.push({ name: 'attempt', params: { id: a.id } })">Làm tiếp</el-button>
      </div>
    </el-card>

    <StatsDashboard v-if="stats" :stats="stats" class="section" @practice-topic="practiceTopic" />

    <el-card v-if="stats?.recent.length" shadow="never" class="section">
      <template #header>
        <div class="recent-header">
          <span>Bài gần đây</span>
          <RouterLink to="/history" class="small">Xem tất cả</RouterLink>
        </div>
      </template>
      <el-table :data="stats.recent" size="small" @row-click="(row: { id: string }) => router.push({ name: 'attempt-result', params: { id: row.id } })">
        <el-table-column label="Bài" min-width="200">
          <template #default="{ row }">{{ row.title }}</template>
        </el-table-column>
        <el-table-column label="Chế độ" width="110">
          <template #default="{ row }">{{ modeLabels[row.mode as keyof typeof modeLabels] }}</template>
        </el-table-column>
        <el-table-column label="Điểm" width="90" align="right">
          <template #default="{ row }">{{ formatScore(row.score) }}</template>
        </el-table-column>
        <el-table-column label="Nộp lúc" width="140">
          <template #default="{ row }">{{ row.submittedAt ? formatDateTime(row.submittedAt) : '' }}</template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.actions {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 12px;
  margin-bottom: 16px;
}

.action {
  cursor: pointer;
}

.action :deep(.el-card__body) {
  display: flex;
  align-items: center;
  gap: 12px;
}

.action .el-icon {
  color: var(--el-color-primary);
}

.section {
  margin-bottom: 16px;
}

.small {
  font-size: 12px;
}

.in-progress {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 8px 0;
}

.in-progress + .in-progress {
  border-top: 1px solid var(--app-border);
}

.ip-info {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
}

.recent-header {
  display: flex;
  justify-content: space-between;
}

:deep(.el-table__row) {
  cursor: pointer;
}
</style>
