<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete } from '@element-plus/icons-vue'
import { attemptsApi } from '@/api/attempts'
import type { AttemptMode, AttemptSummary, PagedResult } from '@/api/types'
import { getErrorMessage } from '@/utils/errors'
import { formatDateTime, formatScore, modeLabels } from '@/utils/labels'

const router = useRouter()
const query = reactive({ mode: undefined as AttemptMode | undefined, page: 1, pageSize: 20 })
const result = ref<PagedResult<AttemptSummary>>({ items: [], total: 0, page: 1, pageSize: 20 })
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    result.value = await attemptsApi.list(query)
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
}
onMounted(load)

function changeMode(mode: AttemptMode | undefined) {
  query.mode = mode
  query.page = 1
  load()
}

function open(row: AttemptSummary) {
  router.push(row.status === 'InProgress' ? { name: 'attempt', params: { id: row.id } } : { name: 'attempt-result', params: { id: row.id } })
}

async function remove(row: AttemptSummary) {
  try {
    await ElMessageBox.confirm(`Xóa bài "${row.title}" khỏi lịch sử? Thống kê ôn tập vẫn được giữ.`, 'Xóa bài làm', {
      type: 'warning',
      confirmButtonText: 'Xóa',
      cancelButtonText: 'Hủy',
    })
  } catch {
    return
  }
  try {
    await attemptsApi.remove(row.id)
    ElMessage.success('Đã xóa')
    await load()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
}
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Lịch sử làm bài</h1>
      <div class="spacer" />
      <el-radio-group :model-value="query.mode ?? ''" size="small" @update:model-value="(v) => changeMode((v || undefined) as AttemptMode | undefined)">
        <el-radio-button value="">Tất cả</el-radio-button>
        <el-radio-button v-for="(label, mode) in modeLabels" :key="mode" :value="mode">{{ label }}</el-radio-button>
      </el-radio-group>
    </div>

    <el-card shadow="never">
      <el-table v-loading="loading" :data="result.items" empty-text="Chưa có bài làm nào" row-class-name="clickable" @row-click="open">
        <el-table-column label="Bài" min-width="220">
          <template #default="{ row }">
            <div>{{ row.title }}</div>
            <div class="muted small">{{ modeLabels[row.mode as AttemptMode] }} · {{ formatDateTime(row.startedAt) }}</div>
          </template>
        </el-table-column>
        <el-table-column label="Trạng thái" width="130">
          <template #default="{ row }">
            <el-tag v-if="row.status === 'InProgress'" size="small" type="warning" effect="plain">Đang làm</el-tag>
            <el-tag v-else-if="row.passed === true" size="small" type="success">✓ Đạt</el-tag>
            <el-tag v-else-if="row.passed === false" size="small" type="danger">✗ Chưa đạt</el-tag>
            <el-tag v-else size="small" type="info" effect="plain">Đã nộp</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="Kết quả" width="140" align="right">
          <template #default="{ row }">
            <div class="num">{{ formatScore(row.score) }}</div>
            <div class="muted small num">{{ row.correctCount }} đúng / {{ row.answeredCount }}/{{ row.totalQuestions }} làm</div>
          </template>
        </el-table-column>
        <el-table-column width="60" align="right">
          <template #default="{ row }">
            <el-button link type="danger" :icon="Delete" aria-label="Xóa" @click.stop="remove(row as AttemptSummary)" />
          </template>
        </el-table-column>
      </el-table>

      <el-pagination
        v-if="result.total > query.pageSize"
        v-model:current-page="query.page"
        class="pagination"
        background
        layout="prev, pager, next"
        :total="result.total"
        :page-size="query.pageSize"
        @current-change="load"
      />
    </el-card>
  </div>
</template>

<style scoped>
.small {
  font-size: 12px;
}

.num {
  font-variant-numeric: tabular-nums;
}

.pagination {
  margin-top: 16px;
  justify-content: flex-end;
}

:deep(.clickable) {
  cursor: pointer;
}
</style>
