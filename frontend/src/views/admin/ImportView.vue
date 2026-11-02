<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, type TableInstance, type UploadFile, type UploadInstance, type UploadRawFile } from 'element-plus'
import { Download, UploadFilled } from '@element-plus/icons-vue'
import { importApi } from '@/api/admin'
import { subjectsApi } from '@/api/subjects'
import type { ImportCommitResult, ImportItem, ImportPreview, Subject } from '@/api/types'
import MarkdownView from '@/components/MarkdownView.vue'
import { getErrorMessage } from '@/utils/errors'
import { difficultyMeta, optionLetter, questionTypeLabel, subjectCascaderOptions } from '@/utils/labels'
import { hasCodeBlock, toPlainPreview } from '@/utils/markdown'

const router = useRouter()
const step = ref<'upload' | 'preview' | 'done'>('upload')
const subjects = ref<Subject[]>([])
const uploadRef = ref<UploadInstance>()
const file = ref<File | null>(null)
const defaultLocation = ref<number[]>([])
const loading = ref(false)
const preview = ref<ImportPreview | null>(null)
const tableRef = ref<TableInstance>()
const selection = ref<ImportItem[]>([])
const result = ref<ImportCommitResult | null>(null)

const defaultTopicId = computed(() => defaultLocation.value[1])
const cascaderOptions = computed(() => subjectCascaderOptions(subjects.value, true))

onMounted(async () => {
  try {
    subjects.value = await subjectsApi.list()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
})

function onFileChange(uploadFile: UploadFile) {
  file.value = uploadFile.raw ?? null
}

function onExceed(files: File[]) {
  // Chọn file khác thì thay file cũ
  uploadRef.value?.clearFiles()
  const next = files[0] as UploadRawFile
  uploadRef.value?.handleStart(next)
  file.value = next
}

async function download(kind: 'excel' | 'word') {
  try {
    await importApi.downloadTemplate(kind)
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
}

const isSelectable = (row: ImportItem) => row.errors.length === 0 && row.duplicateOfQuestionId == null && row.duplicateOfIndex == null

async function runPreview() {
  if (!file.value) {
    ElMessage.warning('Hãy chọn file.')
    return
  }
  loading.value = true
  try {
    preview.value = await importApi.preview(file.value, defaultTopicId.value)
    step.value = 'preview'
    await nextTick()
    for (const row of preview.value.items) if (isSelectable(row)) tableRef.value?.toggleRowSelection(row, true)
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
}

const counts = computed(() => {
  const items = preview.value?.items ?? []
  const newTopics = new Set(items.filter((i) => i.createsTopic && i.errors.length === 0).map((i) => `${i.subjectName}/${i.topicName}`))
  return {
    total: items.length,
    valid: items.filter(isSelectable).length,
    errors: items.filter((i) => i.errors.length > 0).length,
    duplicates: items.filter((i) => i.duplicateOfQuestionId != null || i.duplicateOfIndex != null).length,
    warnings: items.filter((i) => i.warnings.length > 0).length,
    newTopics: [...newTopics],
  }
})

async function commit() {
  if (selection.value.length === 0) {
    ElMessage.warning('Chưa chọn câu nào.')
    return
  }
  loading.value = true
  try {
    result.value = await importApi.commit(selection.value, defaultTopicId.value)
    step.value = 'done'
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
}

function reset() {
  step.value = 'upload'
  preview.value = null
  result.value = null
  selection.value = []
  file.value = null
  uploadRef.value?.clearFiles()
}

const correctLetters = (row: ImportItem) =>
  row.options.map((o, i) => (o.isCorrect ? optionLetter(i) : null)).filter(Boolean).join(', ') || '—'
</script>

<template>
  <div v-loading="loading">
    <div class="page-header">
      <h1>Import câu hỏi</h1>
      <div class="spacer" />
      <el-steps :active="['upload', 'preview', 'done'].indexOf(step)" finish-status="success" simple class="steps">
        <el-step title="Chọn file" />
        <el-step title="Xem trước" />
        <el-step title="Hoàn tất" />
      </el-steps>
    </div>

    <!-- Bước 1 -->
    <el-card v-if="step === 'upload'" shadow="never">
      <div class="templates">
        <span class="muted">Chưa có file? Tải mẫu về điền theo:</span>
        <el-button :icon="Download" @click="download('excel')">Mẫu Excel</el-button>
        <el-button :icon="Download" @click="download('word')">Mẫu Word</el-button>
      </div>

      <el-upload
        ref="uploadRef"
        drag
        :auto-upload="false"
        :limit="1"
        accept=".xlsx,.docx"
        :on-change="onFileChange"
        :on-remove="() => (file = null)"
        :on-exceed="onExceed"
      >
        <el-icon class="el-icon--upload"><UploadFilled /></el-icon>
        <div class="el-upload__text">Kéo thả file vào đây hoặc <em>bấm để chọn</em></div>
        <template #tip>
          <div class="el-upload__tip">
            Hỗ trợ Excel (.xlsx) và Word (.docx), tối đa 10 MB. Trong Word: đoạn font Consolas/Courier được nhận là code, Heading là tên chủ đề,
            đáp án có thể ghi trong từng câu hoặc gom ở mục "ĐÁP ÁN" cuối file (dạng "Câu 1 — A. giải thích").
          </div>
        </template>
      </el-upload>

      <el-form label-position="top" class="options">
        <el-form-item label="Chủ đề mặc định (cho câu không ghi môn/chủ đề)">
          <el-cascader v-model="defaultLocation" :options="cascaderOptions" clearable filterable placeholder="Không dùng" class="full" />
        </el-form-item>
      </el-form>
      <el-button type="primary" size="large" :disabled="!file" @click="runPreview">Đọc file & xem trước</el-button>
    </el-card>

    <!-- Bước 2 -->
    <template v-if="step === 'preview' && preview">
      <el-card shadow="never" class="section">
        <div class="summary">
          <div><strong>{{ preview.fileName }}</strong></div>
          <el-tag type="info">{{ counts.total }} câu</el-tag>
          <el-tag type="success">{{ counts.valid }} hợp lệ</el-tag>
          <el-tag v-if="counts.errors" type="danger">{{ counts.errors }} lỗi</el-tag>
          <el-tag v-if="counts.duplicates" type="info" effect="plain">{{ counts.duplicates }} trùng</el-tag>
          <el-tag v-if="counts.warnings" type="warning">{{ counts.warnings }} cảnh báo</el-tag>
        </div>
        <el-alert
          v-if="counts.newTopics.length"
          type="info"
          :closable="false"
          show-icon
          class="new-topics"
          :title="`Sẽ tạo mới ${counts.newTopics.length} chủ đề: ${counts.newTopics.join(', ')}`"
        />
      </el-card>

      <el-card shadow="never" class="section">
        <el-table ref="tableRef" :data="preview.items" row-key="index" @selection-change="(rows: ImportItem[]) => (selection = rows)">
          <el-table-column type="selection" width="44" :selectable="isSelectable" />
          <el-table-column type="expand" width="36">
            <template #default="{ row }">
              <div class="expand">
                <ul v-if="row.errors.length" class="issues errors">
                  <li v-for="e in row.errors" :key="e">{{ e }}</li>
                </ul>
                <ul v-if="row.warnings.length" class="issues warnings">
                  <li v-for="w in row.warnings" :key="w">{{ w }}</li>
                </ul>
                <MarkdownView :source="row.content || '_(trống)_'" />
                <ul class="preview-options">
                  <li v-for="(o, i) in (row as ImportItem).options" :key="i" :class="{ correct: o.isCorrect }">
                    <strong>{{ optionLetter(i) }}.</strong>
                    <MarkdownView :source="o.content" />
                    <span v-if="o.isCorrect" class="correct-label">✓ đúng</span>
                  </li>
                </ul>
                <div v-if="row.explanation" class="explanation">
                  <strong>Giải thích:</strong>
                  <MarkdownView :source="row.explanation" />
                </div>
              </div>
            </template>
          </el-table-column>
          <el-table-column prop="source" label="Vị trí" width="90" />
          <el-table-column label="Nội dung" min-width="260">
            <template #default="{ row }">
              {{ toPlainPreview(row.content, 110) || '(trống)' }}
              <el-tag v-if="hasCodeBlock(row.content)" size="small" effect="plain">code</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="Môn / chủ đề" min-width="160">
            <template #default="{ row }">
              <template v-if="row.topicName">
                <div>{{ row.subjectName }} <el-tag v-if="row.createsSubject" size="small" type="warning" effect="plain">mới</el-tag></div>
                <div class="muted small">{{ row.topicName }} <el-tag v-if="row.createsTopic" size="small" type="warning" effect="plain">mới</el-tag></div>
              </template>
              <span v-else class="muted">—</span>
            </template>
          </el-table-column>
          <el-table-column label="Đáp án" width="80">
            <template #default="{ row }">{{ correctLetters(row as ImportItem) }}</template>
          </el-table-column>
          <el-table-column label="Loại / độ khó" width="130">
            <template #default="{ row }">
              <div class="small">{{ questionTypeLabel(row.type) }}</div>
              <el-tag size="small" :type="difficultyMeta(row.difficulty).tag" effect="plain">{{ difficultyMeta(row.difficulty).label }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="Trạng thái" width="150">
            <template #default="{ row }">
              <el-tooltip v-if="row.errors.length" :content="row.errors.join(' ')" placement="top">
                <el-tag type="danger">Lỗi</el-tag>
              </el-tooltip>
              <el-tag v-else-if="row.duplicateOfQuestionId" type="info" effect="plain">Trùng câu #{{ row.duplicateOfQuestionId }}</el-tag>
              <el-tag v-else-if="row.duplicateOfIndex" type="info" effect="plain">Trùng câu thứ {{ row.duplicateOfIndex }}</el-tag>
              <el-tooltip v-else-if="row.warnings.length" :content="row.warnings.join(' ')" placement="top">
                <el-tag type="warning">Cảnh báo</el-tag>
              </el-tooltip>
              <el-tag v-else type="success">Hợp lệ</el-tag>
            </template>
          </el-table-column>
        </el-table>
      </el-card>

      <div class="footer">
        <el-button @click="reset">Chọn file khác</el-button>
        <el-button type="primary" :disabled="selection.length === 0" @click="commit">Lưu {{ selection.length }} câu đã chọn</el-button>
      </div>
    </template>

    <!-- Bước 3 -->
    <el-card v-if="step === 'done' && result" shadow="never">
      <el-result icon="success" :title="`Đã thêm ${result.created} câu hỏi`">
        <template #sub-title>
          <span v-if="result.subjectsCreated || result.topicsCreated">
            Tạo mới {{ result.subjectsCreated }} môn, {{ result.topicsCreated }} chủ đề.
          </span>
          <span v-if="result.skipped"> Bỏ qua {{ result.skipped }} câu.</span>
        </template>
        <template #extra>
          <el-button type="primary" @click="router.push('/admin/questions')">Xem ngân hàng câu hỏi</el-button>
          <el-button @click="reset">Import file khác</el-button>
        </template>
      </el-result>
      <el-collapse v-if="result.skippedReasons.length">
        <el-collapse-item :title="`Lý do bỏ qua (${result.skippedReasons.length})`">
          <ul class="issues">
            <li v-for="r in result.skippedReasons" :key="r">{{ r }}</li>
          </ul>
        </el-collapse-item>
      </el-collapse>
    </el-card>
  </div>
</template>

<style scoped>
.steps {
  flex: 1 1 440px;
  max-width: 540px;
}

.steps :deep(.el-step__title) {
  white-space: nowrap;
}

.templates {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
}

.templates .el-button + .el-button {
  margin-left: 0;
}

.options {
  max-width: 480px;
  margin-top: 16px;
}

.full {
  width: 100%;
}

.section {
  margin-bottom: 16px;
}

.summary {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.new-topics {
  margin-top: 12px;
}

.small {
  font-size: 12px;
}

.expand {
  padding: 4px 16px 12px 48px;
}

.issues {
  margin: 0 0 8px;
  padding-left: 18px;
  font-size: 13px;
}

.issues.errors {
  color: var(--status-critical);
}

.issues.warnings {
  color: var(--el-color-warning-dark-2);
}

.preview-options {
  list-style: none;
  padding: 0;
  margin: 8px 0;
}

.preview-options li {
  display: flex;
  gap: 8px;
  padding: 4px 8px;
  border-radius: 6px;
}

.preview-options li.correct {
  background: var(--app-correct-bg);
}

.correct-label {
  margin-left: auto;
  color: var(--status-good-text);
  font-size: 12px;
  white-space: nowrap;
}

.explanation {
  font-size: 14px;
}

.footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
</style>
