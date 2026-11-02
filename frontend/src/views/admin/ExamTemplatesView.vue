<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, Edit, Plus, VideoPlay, WarningFilled } from '@element-plus/icons-vue'
import { attemptsApi } from '@/api/attempts'
import { examTemplatesApi } from '@/api/examTemplates'
import { subjectsApi } from '@/api/subjects'
import type { Difficulty, ExamRule, ExamTemplate, Subject } from '@/api/types'
import { getErrorMessage } from '@/utils/errors'
import { difficultyMeta, difficultyOptions, formatDateTime, subjectCascaderOptions } from '@/utils/labels'

const router = useRouter()
const templates = ref<ExamTemplate[]>([])
const subjects = ref<Subject[]>([])
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    ;[templates.value, subjects.value] = await Promise.all([examTemplatesApi.list(), subjectsApi.list()])
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
}
onMounted(load)

// ---------- Form ----------
interface RuleForm {
  key: number
  location: number[]
  difficulty: Difficulty | null
  questionCount: number
}
let keySeq = 0
const newRule = (location: number[] = [], difficulty: Difficulty | null = null, questionCount = 10): RuleForm => ({
  key: ++keySeq,
  location,
  difficulty,
  questionCount,
})

const dialog = reactive({ visible: false, saving: false, editingId: null as number | null })
const form = reactive({
  title: '',
  description: '',
  durationMinutes: 60,
  passScore: 70,
  shuffleOptions: true,
  isPublished: true,
  rules: [] as RuleForm[],
})
const cascaderOptions = computed(() => subjectCascaderOptions(subjects.value))
const totalQuestions = computed(() => form.rules.reduce((n, r) => n + (r.questionCount || 0), 0))

/** Số câu có trong phạm vi (chưa lọc độ khó) để gợi ý khi soạn ma trận. */
function poolSize(location: number[]) {
  const subject = subjects.value.find((s) => s.id === location[0])
  if (!subject) return null
  return location[1] ? (subject.topics.find((t) => t.id === location[1])?.questionCount ?? 0) : subject.questionCount
}

function open(template?: ExamTemplate) {
  dialog.editingId = template?.id ?? null
  Object.assign(form, {
    title: template?.title ?? '',
    description: template?.description ?? '',
    durationMinutes: template?.durationMinutes ?? 60,
    passScore: template?.passScore ?? 70,
    shuffleOptions: template?.shuffleOptions ?? true,
    isPublished: template?.isPublished ?? true,
    rules: template
      ? template.rules.map((r) =>
          newRule(r.topicId ? [r.subjectId!, r.topicId] : [r.subjectId!], r.difficulty, r.questionCount),
        )
      : [newRule()],
  })
  dialog.visible = true
}

function validate(): string | null {
  if (!form.title.trim()) return 'Nhập tên đề thi.'
  if (form.rules.length === 0) return 'Thêm ít nhất một dòng ma trận.'
  if (form.rules.some((r) => r.location.length === 0)) return 'Mỗi dòng ma trận phải chọn môn hoặc chủ đề.'
  return null
}

async function save() {
  const error = validate()
  if (error) {
    ElMessage.warning(error)
    return
  }
  dialog.saving = true
  try {
    const body = {
      title: form.title,
      description: form.description.trim() || null,
      durationMinutes: form.durationMinutes,
      passScore: form.passScore,
      shuffleOptions: form.shuffleOptions,
      isPublished: form.isPublished,
      rules: form.rules.map((r) => ({
        subjectId: r.location[0] ?? null,
        topicId: r.location[1] ?? null,
        difficulty: r.difficulty,
        questionCount: r.questionCount,
      })),
    }
    const saved = dialog.editingId
      ? await examTemplatesApi.update(dialog.editingId, body)
      : await examTemplatesApi.create(body)
    dialog.visible = false
    if (saved.hasEnoughQuestions) ElMessage.success('Đã lưu đề thi')
    else ElMessage.warning('Đã lưu, nhưng ngân hàng chưa đủ câu cho một số dòng ma trận.')
    await load()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    dialog.saving = false
  }
}

async function remove(t: ExamTemplate) {
  try {
    await ElMessageBox.confirm(`Xóa đề "${t.title}"? Lịch sử bài thi vẫn được giữ.`, 'Xóa đề thi', {
      type: 'warning',
      confirmButtonText: 'Xóa',
      cancelButtonText: 'Hủy',
    })
  } catch {
    return
  }
  try {
    await examTemplatesApi.remove(t.id)
    ElMessage.success('Đã xóa')
    await load()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
}

async function tryExam(t: ExamTemplate) {
  try {
    const attempt = await attemptsApi.startExam(t.id)
    router.push({ name: 'attempt', params: { id: attempt.id } })
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
}

const ruleScope = (r: ExamRule) =>
  r.topicName ? `${r.subjectName} / ${r.topicName}` : (r.subjectName ?? '(đã xóa)')
</script>

<template>
  <div v-loading="loading">
    <div class="page-header">
      <h1>Đề thi</h1>
      <div class="spacer" />
      <el-button type="primary" :icon="Plus" @click="open()">Tạo đề thi</el-button>
    </div>

    <el-card shadow="never">
      <p class="muted intro">
        Mỗi đề là một <strong>ma trận</strong>: mỗi lần thi, hệ thống bốc ngẫu nhiên số câu theo từng dòng (môn/chủ đề, độ khó), nên mỗi lần là một đề khác.
      </p>
      <el-table :data="templates" empty-text="Chưa có đề thi nào" row-key="id">
        <el-table-column type="expand" width="36">
          <template #default="{ row }">
            <el-table :data="row.rules" size="small" class="rules-table">
              <el-table-column label="Phạm vi" min-width="200">
                <template #default="{ row: r }">{{ ruleScope(r as ExamRule) }}</template>
              </el-table-column>
              <el-table-column label="Độ khó" width="120">
                <template #default="{ row: r }">{{ r.difficulty ? difficultyMeta(r.difficulty).label : 'Tất cả' }}</template>
              </el-table-column>
              <el-table-column label="Cần / có sẵn" width="130" align="right">
                <template #default="{ row: r }">
                  <span :class="{ short: r.availableCount < r.questionCount }">{{ r.questionCount }} / {{ r.availableCount }}</span>
                </template>
              </el-table-column>
            </el-table>
          </template>
        </el-table-column>
        <el-table-column label="Tên đề" min-width="220">
          <template #default="{ row }">
            <div>{{ row.title }}</div>
            <div v-if="row.description" class="muted small">{{ row.description }}</div>
          </template>
        </el-table-column>
        <el-table-column label="Số câu" width="90" align="right">
          <template #default="{ row }">{{ row.totalQuestions }}</template>
        </el-table-column>
        <el-table-column label="Thời gian" width="100" align="right">
          <template #default="{ row }">{{ row.durationMinutes }} phút</template>
        </el-table-column>
        <el-table-column label="Đạt" width="70" align="right">
          <template #default="{ row }">{{ row.passScore }}%</template>
        </el-table-column>
        <el-table-column label="Trạng thái" width="150">
          <template #default="{ row }">
            <el-tag v-if="row.isPublished" size="small" type="success" effect="plain">Phát hành</el-tag>
            <el-tag v-else size="small" type="info" effect="plain">Nháp</el-tag>
            <div v-if="!row.hasEnoughQuestions" class="short small"><el-icon><WarningFilled /></el-icon> Thiếu câu</div>
          </template>
        </el-table-column>
        <el-table-column label="Cập nhật" width="130">
          <template #default="{ row }"><span class="small">{{ formatDateTime(row.updatedAt) }}</span></template>
        </el-table-column>
        <el-table-column width="120" align="right" fixed="right">
          <template #default="{ row }">
            <el-button link :icon="VideoPlay" title="Thi thử ngay" aria-label="Thi thử" @click="tryExam(row as ExamTemplate)" />
            <el-button link :icon="Edit" aria-label="Sửa" @click="open(row as ExamTemplate)" />
            <el-button link type="danger" :icon="Delete" aria-label="Xóa" @click="remove(row as ExamTemplate)" />
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="dialog.visible" :title="dialog.editingId ? 'Sửa đề thi' : 'Tạo đề thi'" width="min(860px, 96vw)" top="5vh">
      <el-form label-position="top" novalidate @submit.prevent="save">
        <el-form-item label="Tên đề" required>
          <el-input v-model="form.title" maxlength="200" placeholder="Ví dụ: Đề thi tuyển .NET Developer" />
        </el-form-item>
        <el-form-item label="Mô tả">
          <el-input v-model="form.description" type="textarea" :rows="2" maxlength="2000" />
        </el-form-item>
        <div class="inline">
          <el-form-item label="Thời gian (phút)">
            <el-input-number v-model="form.durationMinutes" :min="1" :max="600" />
          </el-form-item>
          <el-form-item label="Điểm đạt (%)">
            <el-input-number v-model="form.passScore" :min="0" :max="100" :step="5" />
          </el-form-item>
          <el-form-item label="Tùy chọn">
            <div class="switches">
              <el-switch v-model="form.shuffleOptions" active-text="Xáo thứ tự đáp án" />
              <el-switch v-model="form.isPublished" active-text="Phát hành" />
            </div>
          </el-form-item>
        </div>

        <div class="rules-header">
          <strong>Ma trận đề</strong>
          <span class="muted small">Tổng {{ totalQuestions }} câu</span>
        </div>
        <div v-for="(r, i) in form.rules" :key="r.key" class="rule-row">
          <el-cascader
            v-model="r.location"
            :options="cascaderOptions"
            :props="{ checkStrictly: true }"
            placeholder="Môn hoặc chủ đề"
            filterable
            class="rule-scope"
          />
          <el-select v-model="r.difficulty" placeholder="Mọi độ khó" clearable class="rule-difficulty">
            <el-option v-for="d in difficultyOptions" :key="d.value" :value="d.value" :label="d.label" />
          </el-select>
          <el-input-number v-model="r.questionCount" :min="1" :max="500" class="rule-count" />
          <span class="muted small pool">
            <template v-if="poolSize(r.location) != null">có {{ poolSize(r.location) }} câu{{ r.difficulty ? ' (mọi độ khó)' : '' }}</template>
          </span>
          <el-button text type="danger" :icon="Delete" :disabled="form.rules.length === 1" :aria-label="`Xóa dòng ${i + 1}`" @click="form.rules.splice(i, 1)" />
        </div>
        <el-button :icon="Plus" @click="form.rules.push(newRule())">Thêm dòng</el-button>
      </el-form>
      <template #footer>
        <el-button @click="dialog.visible = false">Hủy</el-button>
        <el-button type="primary" :loading="dialog.saving" @click="save">Lưu</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.intro {
  margin-top: 0;
  font-size: 14px;
}

.small {
  font-size: 12px;
}

.short {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  color: var(--status-critical);
}

.rules-table {
  margin: 0 16px 8px 48px;
  width: auto;
}

.inline {
  display: flex;
  flex-wrap: wrap;
  column-gap: 24px;
}

.switches {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
}

.rules-header {
  display: flex;
  align-items: baseline;
  gap: 12px;
  margin-bottom: 8px;
}

.rule-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.rule-scope {
  flex: 1 1 260px;
}

.rule-difficulty {
  flex: 0 1 150px;
}

.rule-count {
  width: 130px;
}

.pool {
  min-width: 110px;
}
</style>
