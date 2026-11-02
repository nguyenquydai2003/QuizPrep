<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, Edit, Plus, Search } from '@element-plus/icons-vue'
import { questionsApi } from '@/api/questions'
import { subjectsApi } from '@/api/subjects'
import type { Difficulty, PagedResult, QuestionListItem, QuestionQuery, QuestionType, Subject } from '@/api/types'
import { getErrorMessage } from '@/utils/errors'
import { difficultyMeta, difficultyOptions, formatDateTime, questionTypeLabel, questionTypeOptions } from '@/utils/labels'
import { hasCodeBlock, toPlainPreview } from '@/utils/markdown'

const DEFAULT_PAGE_SIZE = 20

const route = useRoute()
const router = useRouter()

const subjects = ref<Subject[]>([])
const tags = ref<string[]>([])
const result = ref<PagedResult<QuestionListItem>>({ items: [], total: 0, page: 1, pageSize: DEFAULT_PAGE_SIZE })
const loading = ref(false)

// Bộ lọc lưu trên URL để quay lại từ trang sửa vẫn giữ nguyên
const positiveInt = (v: unknown) => {
  const n = Number(v)
  return Number.isInteger(n) && n > 0 ? n : undefined
}
const nonEmpty = (v: unknown) => (typeof v === 'string' && v ? v : undefined)

const query = computed<QuestionQuery>(() => ({
  subjectId: positiveInt(route.query.subjectId),
  topicId: positiveInt(route.query.topicId),
  difficulty: nonEmpty(route.query.difficulty) as Difficulty | undefined,
  type: nonEmpty(route.query.type) as QuestionType | undefined,
  tag: nonEmpty(route.query.tag),
  search: nonEmpty(route.query.search),
  page: positiveInt(route.query.page) ?? 1,
  pageSize: positiveInt(route.query.pageSize) ?? DEFAULT_PAGE_SIZE,
}))

function updateQuery(patch: Partial<QuestionQuery>) {
  // Đổi bộ lọc thì quay về trang 1
  const next: QuestionQuery = { ...query.value, page: 1, ...patch }
  const q: Record<string, string> = {}
  for (const [key, value] of Object.entries(next)) {
    if (value === undefined || value === null || value === '') continue
    if (key === 'page' && value === 1) continue
    if (key === 'pageSize' && value === DEFAULT_PAGE_SIZE) continue
    q[key] = String(value)
  }
  router.replace({ query: q })
}

const cascaderOptions = computed(() =>
  subjects.value.map((s) => ({
    value: s.id,
    label: s.name,
    children: s.topics.map((t) => ({ value: t.id, label: t.name })),
  })),
)

const location = computed<number[]>({
  get() {
    const { subjectId, topicId } = query.value
    if (topicId) {
      const subject = subjects.value.find((s) => s.topics.some((t) => t.id === topicId))
      return subject ? [subject.id, topicId] : []
    }
    return subjectId ? [subjectId] : []
  },
  set(value) {
    updateQuery({ subjectId: value?.[0], topicId: value?.[1] })
  },
})

const searchText = ref(query.value.search ?? '')
let searchTimer: number | undefined
watch(searchText, (value) => {
  window.clearTimeout(searchTimer)
  searchTimer = window.setTimeout(() => updateQuery({ search: value.trim() || undefined }), 350)
})

let loadSeq = 0
async function load() {
  const seq = ++loadSeq
  loading.value = true
  try {
    const data = await questionsApi.search(query.value)
    if (seq === loadSeq) result.value = data
  } catch (e) {
    if (seq === loadSeq) ElMessage.error(getErrorMessage(e))
  } finally {
    if (seq === loadSeq) loading.value = false
  }
}

watch(
  () => route.query,
  () => {
    if (route.name === 'admin-questions') load()
  },
  { immediate: true },
)

onMounted(async () => {
  try {
    ;[subjects.value, tags.value] = await Promise.all([subjectsApi.list(), questionsApi.tags()])
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
})

function createQuestion() {
  const { topicId } = query.value
  router.push({ name: 'admin-question-create', query: topicId ? { topicId } : {} })
}

function editQuestion(row: QuestionListItem) {
  router.push({ name: 'admin-question-edit', params: { id: row.id } })
}

async function removeQuestion(row: QuestionListItem) {
  try {
    await ElMessageBox.confirm(`Xóa câu hỏi #${row.id}?`, 'Xác nhận xóa', {
      type: 'warning',
      confirmButtonText: 'Xóa',
      cancelButtonText: 'Hủy',
    })
  } catch {
    return
  }
  try {
    await questionsApi.remove(row.id)
    ElMessage.success('Đã xóa')
    // Xóa câu cuối cùng của trang thì lùi về trang trước
    if (result.value.items.length === 1 && query.value.page > 1) updateQuery({ page: query.value.page - 1 })
    else await load()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
}
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Câu hỏi</h1>
      <el-tag type="info">{{ result.total }}</el-tag>
      <div class="spacer" />
      <el-button type="primary" :icon="Plus" @click="createQuestion">Thêm câu hỏi</el-button>
    </div>

    <el-card shadow="never" class="filters">
      <el-input v-model="searchText" :prefix-icon="Search" placeholder="Tìm trong nội dung..." clearable class="f-search" />
      <el-cascader
        v-model="location"
        :options="cascaderOptions"
        :props="{ checkStrictly: true }"
        placeholder="Môn / chủ đề"
        clearable
        class="f-location"
      />
      <el-select
        :model-value="query.difficulty"
        placeholder="Độ khó"
        clearable
        class="f-small"
        @update:model-value="(v: Difficulty | undefined) => updateQuery({ difficulty: v || undefined })"
      >
        <el-option v-for="d in difficultyOptions" :key="d.value" :value="d.value" :label="d.label" />
      </el-select>
      <el-select
        :model-value="query.type"
        placeholder="Loại"
        clearable
        class="f-small"
        @update:model-value="(v: QuestionType | undefined) => updateQuery({ type: v || undefined })"
      >
        <el-option v-for="t in questionTypeOptions" :key="t.value" :value="t.value" :label="t.label" />
      </el-select>
      <el-select
        :model-value="query.tag"
        placeholder="Tag"
        clearable
        filterable
        class="f-small"
        @update:model-value="(v: string | undefined) => updateQuery({ tag: v || undefined })"
      >
        <el-option v-for="t in tags" :key="t" :value="t" :label="t" />
      </el-select>
    </el-card>

    <el-card shadow="never">
      <el-table
        v-loading="loading"
        :data="result.items"
        empty-text="Không có câu hỏi nào"
        row-class-name="clickable"
        @row-click="editQuestion"
      >
        <el-table-column prop="id" label="ID" width="70" />
        <el-table-column label="Nội dung" min-width="320">
          <template #default="{ row }">
            <div class="content-cell">
              <span>{{ toPlainPreview(row.contentPreview) }}</span>
              <el-tag v-if="hasCodeBlock(row.contentPreview)" size="small" effect="plain">code</el-tag>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="Môn / chủ đề" min-width="170">
          <template #default="{ row }">
            <div>{{ row.subjectName }}</div>
            <div class="muted small">{{ row.topicName }}</div>
          </template>
        </el-table-column>
        <el-table-column label="Loại" width="120">
          <template #default="{ row }">{{ questionTypeLabel(row.type) }}</template>
        </el-table-column>
        <el-table-column label="Độ khó" width="110">
          <template #default="{ row }">
            <el-tag :type="difficultyMeta(row.difficulty).tag" size="small">{{ difficultyMeta(row.difficulty).label }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="Tags" min-width="140">
          <template #default="{ row }">
            <el-tag v-for="t in row.tags" :key="t" size="small" type="info" class="tag">{{ t }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="Cập nhật" width="140">
          <template #default="{ row }">
            <span class="small">{{ formatDateTime(row.updatedAt) }}</span>
          </template>
        </el-table-column>
        <el-table-column width="96" align="right" fixed="right">
          <template #default="{ row }">
            <el-button link :icon="Edit" aria-label="Sửa" @click.stop="editQuestion(row as QuestionListItem)" />
            <el-button
              link
              type="danger"
              :icon="Delete"
              aria-label="Xóa"
              @click.stop="removeQuestion(row as QuestionListItem)"
            />
          </template>
        </el-table-column>
      </el-table>

      <el-pagination
        v-if="result.total > 0"
        class="pagination"
        background
        layout="total, sizes, prev, pager, next"
        :total="result.total"
        :current-page="query.page"
        :page-size="query.pageSize"
        :page-sizes="[10, 20, 50, 100]"
        @current-change="(page: number) => updateQuery({ page })"
        @size-change="(pageSize: number) => updateQuery({ pageSize })"
      />
    </el-card>
  </div>
</template>

<style scoped>
.filters {
  margin-bottom: 16px;
}

.filters :deep(.el-card__body) {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.f-search {
  flex: 1 1 240px;
}

.f-location {
  flex: 0 1 240px;
}

.f-small {
  flex: 0 1 150px;
}

.content-cell {
  display: flex;
  align-items: flex-start;
  gap: 6px;
}

.small {
  font-size: 12px;
}

.tag {
  margin: 0 4px 4px 0;
}

.pagination {
  margin-top: 16px;
  justify-content: flex-end;
  flex-wrap: wrap;
}

:deep(.clickable) {
  cursor: pointer;
}
</style>
