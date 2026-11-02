<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Delete, Edit, Plus } from '@element-plus/icons-vue'
import { subjectsApi } from '@/api/subjects'
import type { Subject, Topic } from '@/api/types'
import { getErrorMessage } from '@/utils/errors'

const subjects = ref<Subject[]>([])
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    subjects.value = await subjectsApi.list()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
}
onMounted(load)

// Một dialog dùng chung cho cả môn và chủ đề
type DialogTarget = { kind: 'subject'; subject?: Subject } | { kind: 'topic'; subjectId: number; topic?: Topic }

const dialog = reactive({ visible: false, saving: false, target: null as DialogTarget | null })
const formRef = ref<FormInstance>()
const form = reactive({ name: '', description: '', sortOrder: 0 })
const rules: FormRules = {
  name: [
    { required: true, whitespace: true, message: 'Nhập tên', trigger: 'blur' },
    { max: 200, message: 'Tối đa 200 ký tự', trigger: 'blur' },
  ],
}

const dialogTitle = computed(() => {
  const t = dialog.target
  if (!t) return ''
  if (t.kind === 'subject') return t.subject ? 'Sửa môn' : 'Thêm môn'
  return t.topic ? 'Sửa chủ đề' : 'Thêm chủ đề'
})

function openSubject(subject?: Subject) {
  dialog.target = { kind: 'subject', subject }
  Object.assign(form, {
    name: subject?.name ?? '',
    description: subject?.description ?? '',
    sortOrder: subject?.sortOrder ?? subjects.value.length + 1,
  })
  dialog.visible = true
}

function openTopic(subject: Subject, topic?: Topic) {
  dialog.target = { kind: 'topic', subjectId: subject.id, topic }
  Object.assign(form, { name: topic?.name ?? '', description: '', sortOrder: topic?.sortOrder ?? subject.topics.length + 1 })
  dialog.visible = true
}

async function save() {
  const target = dialog.target
  if (!target || !(await formRef.value?.validate().catch(() => false))) return
  dialog.saving = true
  try {
    if (target.kind === 'subject') {
      const body = { name: form.name, description: form.description.trim() || null, sortOrder: form.sortOrder }
      if (target.subject) await subjectsApi.update(target.subject.id, body)
      else await subjectsApi.create(body)
    } else {
      const body = { name: form.name, sortOrder: form.sortOrder }
      if (target.topic) await subjectsApi.updateTopic(target.topic.id, body)
      else await subjectsApi.createTopic(target.subjectId, body)
    }
    dialog.visible = false
    ElMessage.success('Đã lưu')
    await load()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    dialog.saving = false
  }
}

async function confirmDelete(message: string, action: () => Promise<void>) {
  try {
    await ElMessageBox.confirm(message, 'Xác nhận xóa', {
      type: 'warning',
      confirmButtonText: 'Xóa',
      cancelButtonText: 'Hủy',
    })
  } catch {
    return
  }
  try {
    await action()
    ElMessage.success('Đã xóa')
    await load()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
}

const removeSubject = (s: Subject) =>
  confirmDelete(`Xóa môn "${s.name}" cùng toàn bộ chủ đề của nó?`, () => subjectsApi.remove(s.id))
const removeTopic = (t: Topic) => confirmDelete(`Xóa chủ đề "${t.name}"?`, () => subjectsApi.removeTopic(t.id))
</script>

<template>
  <div v-loading="loading">
    <div class="page-header">
      <h1>Môn & chủ đề</h1>
      <div class="spacer" />
      <el-button type="primary" :icon="Plus" @click="openSubject()">Thêm môn</el-button>
    </div>

    <el-empty v-if="!loading && subjects.length === 0" description="Chưa có môn nào. Hãy thêm môn đầu tiên." />

    <el-card v-for="s in subjects" :key="s.id" shadow="never" class="subject">
      <template #header>
        <div class="subject-header">
          <div class="subject-title">
            <strong>{{ s.name }}</strong>
            <el-tag size="small" type="info">{{ s.questionCount }} câu</el-tag>
            <div v-if="s.description" class="muted desc">{{ s.description }}</div>
          </div>
          <div class="actions">
            <el-button size="small" :icon="Plus" @click="openTopic(s)">Chủ đề</el-button>
            <el-button size="small" :icon="Edit" aria-label="Sửa môn" @click="openSubject(s)" />
            <el-button size="small" :icon="Delete" type="danger" plain aria-label="Xóa môn" @click="removeSubject(s)" />
          </div>
        </div>
      </template>

      <el-table :data="s.topics" empty-text="Chưa có chủ đề" size="small">
        <el-table-column prop="name" label="Chủ đề" min-width="200" />
        <el-table-column label="Số câu hỏi" min-width="110">
          <template #default="{ row }">
            <RouterLink :to="{ name: 'admin-questions', query: { topicId: row.id } }">{{ row.questionCount }}</RouterLink>
          </template>
        </el-table-column>
        <el-table-column prop="sortOrder" label="Thứ tự" width="80" />
        <el-table-column width="110" align="right">
          <template #default="{ row }">
            <el-button link :icon="Edit" aria-label="Sửa chủ đề" @click="openTopic(s, row as Topic)" />
            <el-button link type="danger" :icon="Delete" aria-label="Xóa chủ đề" @click="removeTopic(row as Topic)" />
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="dialog.visible" :title="dialogTitle" width="min(460px, 92vw)" @closed="formRef?.clearValidate()">
      <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent="save">
        <el-form-item label="Tên" prop="name">
          <el-input v-model="form.name" maxlength="200" autofocus />
        </el-form-item>
        <el-form-item v-if="dialog.target?.kind === 'subject'" label="Mô tả">
          <el-input v-model="form.description" type="textarea" :rows="2" maxlength="1000" />
        </el-form-item>
        <el-form-item label="Thứ tự hiển thị">
          <el-input-number v-model="form.sortOrder" :min="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialog.visible = false">Hủy</el-button>
        <el-button type="primary" :loading="dialog.saving" @click="save">Lưu</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.subject {
  margin-bottom: 16px;
}

.subject-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.subject-title {
  flex: 1;
  min-width: 200px;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.desc {
  flex-basis: 100%;
  font-size: 13px;
}

.actions {
  display: flex;
  gap: 4px;
}

.actions .el-button + .el-button {
  margin-left: 0;
}
</style>
