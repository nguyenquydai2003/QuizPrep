<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Document, Timer, Trophy } from '@element-plus/icons-vue'
import { attemptsApi } from '@/api/attempts'
import { examTemplatesApi } from '@/api/examTemplates'
import type { AvailableExam } from '@/api/types'
import { useAuthStore } from '@/stores/auth'
import { getErrorMessage } from '@/utils/errors'
import { formatScore } from '@/utils/labels'

const router = useRouter()
const auth = useAuthStore()
const exams = ref<AvailableExam[]>([])
const loading = ref(true)
const starting = ref<number | null>(null)

onMounted(async () => {
  try {
    exams.value = await examTemplatesApi.available()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
})

async function start(exam: AvailableExam) {
  try {
    await ElMessageBox.confirm(
      `Đề gồm ${exam.totalQuestions} câu, thời gian ${exam.durationMinutes} phút. Đồng hồ bắt đầu chạy ngay và vẫn chạy khi bạn rời trang.`,
      exam.title,
      { confirmButtonText: 'Bắt đầu', cancelButtonText: 'Để sau', type: 'info' },
    )
  } catch {
    return
  }
  starting.value = exam.id
  try {
    const attempt = await attemptsApi.startExam(exam.id)
    router.push({ name: 'attempt', params: { id: attempt.id } })
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    starting.value = null
  }
}
</script>

<template>
  <div v-loading="loading">
    <div class="page-header">
      <h1>Thi thử</h1>
    </div>

    <el-empty v-if="!loading && exams.length === 0" description="Chưa có đề thi nào được phát hành.">
      <el-button v-if="auth.isAdmin" type="primary" @click="router.push('/admin/exams')">Tạo đề thi</el-button>
    </el-empty>

    <div class="exams">
      <el-card v-for="e in exams" :key="e.id" shadow="hover" class="exam">
        <h3>{{ e.title }}</h3>
        <p v-if="e.description" class="muted desc">{{ e.description }}</p>
        <div class="facts">
          <span><el-icon><Document /></el-icon>{{ e.totalQuestions }} câu</span>
          <span><el-icon><Timer /></el-icon>{{ e.durationMinutes }} phút</span>
          <span><el-icon><Trophy /></el-icon>Đạt từ {{ e.passScore }}%</span>
        </div>
        <div class="mine muted">
          <template v-if="e.attemptCount">
            Đã thi {{ e.attemptCount }} lần · cao nhất {{ formatScore(e.bestScore) }} · gần nhất {{ formatScore(e.lastScore) }}
          </template>
          <template v-else>Chưa thi lần nào</template>
        </div>
        <el-button type="primary" :loading="starting === e.id" @click="start(e)">Bắt đầu thi</el-button>
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.exams {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

h3 {
  margin: 0 0 6px;
}

.desc {
  margin: 0 0 10px;
  font-size: 14px;
}

.facts {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  font-size: 14px;
}

.facts span {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.mine {
  margin: 10px 0 14px;
  font-size: 13px;
}
</style>
