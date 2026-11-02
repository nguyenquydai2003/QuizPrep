<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft, Check, Delete, MagicStick, Plus } from '@element-plus/icons-vue'
import { aiApi } from '@/api/admin'
import MarkdownTextarea from '@/components/MarkdownTextarea.vue'
import MarkdownView from '@/components/MarkdownView.vue'
import { questionsApi } from '@/api/questions'
import { subjectsApi } from '@/api/subjects'
import type { Difficulty, QuestionType, SaveQuestionRequest, Subject } from '@/api/types'
import { getErrorMessage } from '@/utils/errors'
import { difficultyMeta, difficultyOptions, optionLetter, questionTypeLabel, questionTypeOptions } from '@/utils/labels'

const MIN_OPTIONS = 2
const MAX_OPTIONS = 10

const props = defineProps<{ id?: string }>()
const route = useRoute()
const router = useRouter()

const isEdit = computed(() => props.id !== undefined)
const subjects = ref<Subject[]>([])
const allTags = ref<string[]>([])
const loading = ref(true)
const saving = ref(false)

interface OptionForm {
  key: number
  content: string
  isCorrect: boolean
}
let keySeq = 0
const newOption = (content = '', isCorrect = false): OptionForm => ({ key: ++keySeq, content, isCorrect })

const form = reactive({
  location: [] as number[], // [subjectId, topicId]
  type: 'SingleChoice' as QuestionType,
  difficulty: 'Medium' as Difficulty,
  content: '',
  explanation: '',
  tags: [] as string[],
  options: [newOption(), newOption(), newOption(), newOption()],
})

const isSingle = computed(() => form.type === 'SingleChoice')

const cascaderOptions = computed(() =>
  subjects.value.map((s) => ({
    value: s.id,
    label: s.name,
    // Môn chưa có chủ đề thì không chọn được (câu hỏi phải thuộc một chủ đề)
    children: s.topics.length ? s.topics.map((t) => ({ value: t.id, label: t.name })) : undefined,
    disabled: s.topics.length === 0,
  })),
)

function locationOfTopic(topicId: number): number[] {
  const subject = subjects.value.find((s) => s.topics.some((t) => t.id === topicId))
  return subject ? [subject.id, topicId] : []
}

onMounted(async () => {
  try {
    const [subjectList, tagList] = await Promise.all([subjectsApi.list(), questionsApi.tags()])
    subjects.value = subjectList
    allTags.value = tagList

    if (props.id !== undefined) {
      const q = await questionsApi.get(Number(props.id))
      form.location = [q.subjectId, q.topicId]
      form.type = q.type
      form.difficulty = q.difficulty
      form.content = q.content
      form.explanation = q.explanation ?? ''
      form.tags = [...q.tags]
      form.options = q.options.map((o) => newOption(o.content, o.isCorrect))
    } else {
      const topicId = Number(route.query.topicId)
      if (topicId) form.location = locationOfTopic(topicId)
    }
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
})

// Chuyển sang "một đáp án" thì chỉ giữ lại đáp án đúng đầu tiên
watch(
  () => form.type,
  (type) => {
    if (type !== 'SingleChoice') return
    const first = form.options.findIndex((o) => o.isCorrect)
    form.options.forEach((o, i) => (o.isCorrect = i === first))
  },
)

function toggleCorrect(index: number) {
  if (isSingle.value) form.options.forEach((o, i) => (o.isCorrect = i === index))
  else form.options[index]!.isCorrect = !form.options[index]!.isCorrect
}

function addOption() {
  if (form.options.length < MAX_OPTIONS) form.options.push(newOption())
}

function removeOption(index: number) {
  if (form.options.length > MIN_OPTIONS) form.options.splice(index, 1)
}

function validate(): string | null {
  if (form.location.length !== 2) return 'Chọn môn và chủ đề.'
  if (!form.content.trim()) return 'Nhập nội dung câu hỏi.'
  if (form.options.some((o) => !o.content.trim())) return 'Nội dung đáp án không được để trống.'
  const correct = form.options.filter((o) => o.isCorrect).length
  if (correct === 0) return 'Chọn ít nhất một đáp án đúng (bấm vào chữ cái A, B, C...).'
  if (isSingle.value && correct > 1) return 'Câu một đáp án chỉ được có một đáp án đúng.'
  return null
}

function toRequest(): SaveQuestionRequest {
  return {
    topicId: form.location[1]!,
    type: form.type,
    difficulty: form.difficulty,
    content: form.content,
    explanation: form.explanation.trim() || null,
    tags: form.tags,
    options: form.options.map(({ content, isCorrect }) => ({ content, isCorrect })),
  }
}

function goBack() {
  // Quay lại danh sách với bộ lọc cũ nếu đến từ đó
  const back = window.history.state?.back
  if (typeof back === 'string' && back.startsWith('/admin/questions')) router.back()
  else router.push({ name: 'admin-questions' })
}

function resetForNext() {
  // Giữ lại môn/chủ đề, loại, độ khó để nhập liên tiếp nhanh hơn
  form.content = ''
  form.explanation = ''
  form.tags = []
  form.options = [newOption(), newOption(), newOption(), newOption()]
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

async function save(andNew = false) {
  const error = validate()
  if (error) {
    ElMessage.warning(error)
    return
  }
  saving.value = true
  try {
    if (props.id !== undefined) {
      await questionsApi.update(Number(props.id), toRequest())
      ElMessage.success('Đã cập nhật câu hỏi')
      goBack()
    } else {
      const created = await questionsApi.create(toRequest())
      ElMessage.success(`Đã thêm câu hỏi #${created.id}`)
      for (const t of created.tags) if (!allTags.value.includes(t)) allTags.value.push(t)
      if (andNew) resetForNext()
      else goBack()
    }
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    saving.value = false
  }
}

// ---------- AI gợi ý giải thích ----------
const aiEnabled = ref(false)
const aiLoading = ref(false)
onMounted(async () => {
  try {
    aiEnabled.value = (await aiApi.status()).enabled
  } catch {
    // Không lấy được trạng thái thì coi như tắt
  }
})

async function suggestExplanation() {
  const error = validate()
  if (error) {
    ElMessage.warning(error)
    return
  }
  if (form.explanation.trim()) {
    try {
      await ElMessageBox.confirm('Thay lời giải thích hiện tại bằng gợi ý của AI?', 'Gợi ý bằng AI', {
        confirmButtonText: 'Thay',
        cancelButtonText: 'Hủy',
      })
    } catch {
      return
    }
  }
  aiLoading.value = true
  try {
    form.explanation = await aiApi.explain({
      type: form.type,
      content: form.content,
      options: form.options.map(({ content, isCorrect }) => ({ content, isCorrect })),
    })
    ElMessage.success('Đã tạo gợi ý. Hãy đọc lại trước khi lưu.')
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    aiLoading.value = false
  }
}

// Ctrl+S để lưu
function onKeydown(ev: KeyboardEvent) {
  if ((ev.ctrlKey || ev.metaKey) && ev.key.toLowerCase() === 's') {
    ev.preventDefault()
    if (!saving.value) save()
  }
}
onMounted(() => window.addEventListener('keydown', onKeydown))
onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown))
</script>

<template>
  <div v-loading="loading">
    <div class="page-header">
      <el-button :icon="ArrowLeft" text @click="goBack">Danh sách</el-button>
      <h1>{{ isEdit ? `Sửa câu hỏi #${props.id}` : 'Thêm câu hỏi' }}</h1>
      <div class="spacer" />
      <el-button v-if="!isEdit" :loading="saving" @click="save(true)">Lưu & thêm câu khác</el-button>
      <el-button type="primary" :loading="saving" @click="save()">Lưu (Ctrl+S)</el-button>
    </div>

    <el-row :gutter="20">
      <el-col :xs="24" :lg="13">
        <el-card shadow="never" class="section">
          <el-form label-position="top">
            <el-form-item label="Môn / chủ đề" required>
              <el-cascader
                v-model="form.location"
                :options="cascaderOptions"
                placeholder="Chọn chủ đề"
                filterable
                class="full"
              />
            </el-form-item>
            <div class="inline-fields">
              <el-form-item label="Loại câu hỏi">
                <el-radio-group v-model="form.type">
                  <el-radio-button v-for="t in questionTypeOptions" :key="t.value" :value="t.value">
                    {{ t.label }}
                  </el-radio-button>
                </el-radio-group>
              </el-form-item>
              <el-form-item label="Độ khó">
                <el-radio-group v-model="form.difficulty">
                  <el-radio-button v-for="d in difficultyOptions" :key="d.value" :value="d.value">
                    {{ d.label }}
                  </el-radio-button>
                </el-radio-group>
              </el-form-item>
            </div>
            <el-form-item label="Tags">
              <el-select
                v-model="form.tags"
                multiple
                filterable
                allow-create
                default-first-option
                :reserve-keyword="false"
                placeholder="Gõ để thêm tag mới"
                class="full"
              >
                <el-option v-for="t in allTags" :key="t" :label="t" :value="t" />
              </el-select>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card shadow="never" class="section">
          <MarkdownTextarea
            v-model="form.content"
            label="Nội dung câu hỏi"
            :rows="6"
            placeholder="Hỗ trợ Markdown. Dùng nút “Khối code” để chèn đoạn code."
          />
        </el-card>

        <el-card shadow="never" class="section">
          <template #header>
            <div class="options-header">
              <span>Đáp án</span>
              <span class="muted hint">
                Bấm chữ cái để đánh dấu đáp án đúng{{ isSingle ? '' : ' (được chọn nhiều)' }}
              </span>
            </div>
          </template>

          <div v-for="(o, i) in form.options" :key="o.key" class="option-row" :class="{ correct: o.isCorrect }">
            <el-button
              circle
              :type="o.isCorrect ? 'success' : 'default'"
              :aria-pressed="o.isCorrect"
              :aria-label="`Đánh dấu đáp án ${optionLetter(i)} là đúng`"
              class="letter"
              @click="toggleCorrect(i)"
            >
              {{ optionLetter(i) }}
            </el-button>
            <MarkdownTextarea v-model="o.content" :rows="1" :placeholder="`Đáp án ${optionLetter(i)}`" />
            <el-button
              :icon="Delete"
              text
              type="danger"
              :disabled="form.options.length <= MIN_OPTIONS"
              :aria-label="`Xóa đáp án ${optionLetter(i)}`"
              @click="removeOption(i)"
            />
          </div>

          <el-button :icon="Plus" :disabled="form.options.length >= MAX_OPTIONS" @click="addOption">
            Thêm đáp án
          </el-button>
        </el-card>

        <el-card shadow="never" class="section">
          <MarkdownTextarea
            v-model="form.explanation"
            label="Giải thích (không bắt buộc)"
            :rows="3"
            placeholder="Vì sao đáp án đúng? Hiển thị sau khi làm bài."
          >
            <template #toolbar>
              <el-tooltip :disabled="aiEnabled" content="Cấu hình Anthropic:ApiKey ở backend để bật" placement="top">
                <span>
                  <el-button size="small" text type="primary" :icon="MagicStick" :loading="aiLoading" :disabled="!aiEnabled" @click="suggestExplanation">
                    Gợi ý bằng AI
                  </el-button>
                </span>
              </el-tooltip>
            </template>
          </MarkdownTextarea>
        </el-card>
      </el-col>

      <el-col :xs="24" :lg="11">
        <el-card shadow="never" class="preview">
          <template #header>
            <div class="preview-header">
              <span>Xem trước</span>
              <el-tag size="small" type="info">{{ questionTypeLabel(form.type) }}</el-tag>
              <el-tag size="small" :type="difficultyMeta(form.difficulty).tag">
                {{ difficultyMeta(form.difficulty).label }}
              </el-tag>
            </div>
          </template>

          <MarkdownView v-if="form.content.trim()" :source="form.content" />
          <p v-else class="muted">Nội dung câu hỏi sẽ hiển thị ở đây.</p>

          <ul class="preview-options">
            <li v-for="(o, i) in form.options" :key="o.key" :class="{ correct: o.isCorrect }">
              <span class="preview-letter">{{ optionLetter(i) }}</span>
              <MarkdownView :source="o.content || '…'" class="preview-option-content" />
              <el-icon v-if="o.isCorrect" class="check"><Check /></el-icon>
            </li>
          </ul>

          <div v-if="form.explanation.trim()" class="preview-explanation">
            <div class="explanation-title">Giải thích</div>
            <MarkdownView :source="form.explanation" />
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<style scoped>
.section {
  margin-bottom: 16px;
}

.full {
  width: 100%;
}

.inline-fields {
  display: flex;
  flex-wrap: wrap;
  column-gap: 24px;
}

.options-header {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 8px;
}

.hint {
  font-size: 12px;
}

.option-row {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  margin-bottom: 12px;
}

.option-row .letter {
  flex-shrink: 0;
  margin-top: 4px;
  font-weight: 600;
}

.option-row .el-button + .el-button {
  margin-left: 0;
}

.option-row.correct :deep(.md-textarea) {
  border-color: var(--app-correct-border);
  background: var(--app-correct-bg);
}

.preview {
  position: sticky;
  top: 0;
}

.preview-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.preview-options {
  list-style: none;
  padding: 0;
  margin: 16px 0 0;
}

.preview-options li {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 10px 12px;
  margin-bottom: 8px;
  border: 1px solid var(--app-border);
  border-radius: 8px;
}

.preview-options li.correct {
  border-color: var(--app-correct-border);
  background: var(--app-correct-bg);
}

.preview-letter {
  flex-shrink: 0;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 600;
  background: var(--el-fill-color);
}

.correct .preview-letter {
  background: var(--el-color-success);
  color: #fff;
}

.preview-option-content {
  flex: 1;
  min-width: 0;
}

.check {
  color: var(--el-color-success);
  margin-top: 4px;
}

.preview-explanation {
  margin-top: 16px;
  padding: 12px;
  border-left: 3px solid var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  border-radius: 4px;
}

.explanation-title {
  font-weight: 600;
  margin-bottom: 4px;
}
</style>
