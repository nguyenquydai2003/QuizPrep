<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import { subjectsApi } from '@/api/subjects'
import type { Subject } from '@/api/types'
import { getErrorMessage } from '@/utils/errors'

const subjects = ref<Subject[]>([])
const loading = ref(true)

const stats = computed(() => [
  { label: 'Môn', value: subjects.value.length },
  { label: 'Chủ đề', value: subjects.value.reduce((n, s) => n + s.topics.length, 0) },
  { label: 'Câu hỏi', value: subjects.value.reduce((n, s) => n + s.questionCount, 0) },
])

onMounted(async () => {
  try {
    subjects.value = await subjectsApi.list()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div v-loading="loading">
    <div class="page-header">
      <h1>Tổng quan</h1>
      <div class="spacer" />
      <el-button type="primary" :icon="Plus" @click="$router.push({ name: 'admin-question-create' })">
        Thêm câu hỏi
      </el-button>
    </div>

    <div class="stats">
      <el-card v-for="s in stats" :key="s.label" shadow="never">
        <div class="stat-value">{{ s.value }}</div>
        <div class="muted">{{ s.label }}</div>
      </el-card>
    </div>

    <el-card shadow="never" class="by-subject">
      <template #header>Câu hỏi theo môn</template>
      <el-table :data="subjects" empty-text="Chưa có môn nào">
        <el-table-column prop="name" label="Môn" min-width="180" />
        <el-table-column label="Chủ đề" min-width="120">
          <template #default="{ row }">{{ row.topics.length }}</template>
        </el-table-column>
        <el-table-column prop="questionCount" label="Câu hỏi" min-width="100" />
        <el-table-column width="120" align="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="$router.push({ name: 'admin-questions', query: { subjectId: row.id } })">
              Xem câu hỏi
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style scoped>
.stats {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 16px;
  margin-bottom: 16px;
}

.stat-value {
  font-size: 28px;
  font-weight: 700;
  color: var(--el-color-primary);
}
</style>
