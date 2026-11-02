<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { DataAnalysis, Delete, Edit, Key, Plus } from '@element-plus/icons-vue'
import { usersApi } from '@/api/admin'
import type { Role, UserAdmin } from '@/api/types'
import { useAuthStore } from '@/stores/auth'
import { getErrorMessage } from '@/utils/errors'
import { formatDateTime } from '@/utils/labels'

const router = useRouter()
const auth = useAuthStore()
const users = ref<UserAdmin[]>([])
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    users.value = await usersApi.list()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
}
onMounted(load)

const dialog = reactive({ visible: false, saving: false, editing: null as UserAdmin | null })
const formRef = ref<FormInstance>()
const form = reactive({ userName: '', displayName: '', password: '', role: 'User' as Role })
const rules: FormRules = {
  userName: [
    { required: true, message: 'Nhập tên đăng nhập', trigger: 'blur' },
    { pattern: /^[a-zA-Z0-9._-]{3,50}$/, message: '3-50 ký tự: chữ không dấu, số, . _ -', trigger: 'blur' },
  ],
  displayName: [{ required: true, message: 'Nhập tên hiển thị', trigger: 'blur' }],
  password: [{ required: true, min: 6, message: 'Mật khẩu tối thiểu 6 ký tự', trigger: 'blur' }],
}

function open(user?: UserAdmin) {
  dialog.editing = user ?? null
  Object.assign(form, { userName: user?.userName ?? '', displayName: user?.displayName ?? '', password: '', role: user?.role ?? 'User' })
  dialog.visible = true
}

async function save() {
  if (!(await formRef.value?.validate().catch(() => false))) return
  dialog.saving = true
  try {
    if (dialog.editing) await usersApi.update(dialog.editing.id, { displayName: form.displayName, role: form.role })
    else await usersApi.create({ ...form })
    dialog.visible = false
    ElMessage.success('Đã lưu')
    await load()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    dialog.saving = false
  }
}

async function resetPassword(user: UserAdmin) {
  let value: string
  try {
    ;({ value } = await ElMessageBox.prompt(`Mật khẩu mới cho "${user.userName}"`, 'Đặt lại mật khẩu', {
      inputType: 'password',
      inputValidator: (v) => (v && v.length >= 6) || 'Mật khẩu tối thiểu 6 ký tự',
      confirmButtonText: 'Đặt lại',
      cancelButtonText: 'Hủy',
    }))
  } catch {
    return
  }
  try {
    await usersApi.resetPassword(user.id, value)
    ElMessage.success('Đã đặt lại mật khẩu')
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
}

async function remove(user: UserAdmin) {
  try {
    await ElMessageBox.confirm(`Xóa "${user.userName}" cùng toàn bộ bài làm và thống kê của người này?`, 'Xóa người dùng', {
      type: 'warning',
      confirmButtonText: 'Xóa',
      cancelButtonText: 'Hủy',
    })
  } catch {
    return
  }
  try {
    await usersApi.remove(user.id)
    ElMessage.success('Đã xóa')
    await load()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
}
</script>

<template>
  <div v-loading="loading">
    <div class="page-header">
      <h1>Người dùng</h1>
      <div class="spacer" />
      <el-button type="primary" :icon="Plus" @click="open()">Thêm người dùng</el-button>
    </div>

    <el-card shadow="never">
      <p class="muted intro">
        Muốn cho người khác tự đăng ký, đặt <code>Auth:AllowRegistration = true</code> trong <code>appsettings.json</code> của backend.
      </p>
      <el-table :data="users">
        <el-table-column label="Tài khoản" min-width="180">
          <template #default="{ row }">
            <div>{{ row.displayName }} <el-tag v-if="row.id === auth.user?.id" size="small" effect="plain">bạn</el-tag></div>
            <div class="muted small">{{ row.userName }}</div>
          </template>
        </el-table-column>
        <el-table-column label="Vai trò" width="110">
          <template #default="{ row }">
            <el-tag size="small" :type="row.role === 'Admin' ? 'warning' : 'info'" effect="plain">
              {{ row.role === 'Admin' ? 'Quản trị' : 'Học viên' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="Bài làm" width="90" align="right">
          <template #default="{ row }">{{ row.attemptCount }}</template>
        </el-table-column>
        <el-table-column label="Câu đã làm" width="100" align="right">
          <template #default="{ row }">{{ row.answeredCount }}</template>
        </el-table-column>
        <el-table-column label="Hoạt động gần nhất" width="150">
          <template #default="{ row }">
            <span class="small">{{ row.lastActivityAt ? formatDateTime(row.lastActivityAt) : '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column width="160" align="right" fixed="right">
          <template #default="{ row }">
            <el-button link :icon="DataAnalysis" title="Xem tiến độ" aria-label="Xem tiến độ" @click="router.push({ name: 'admin-user-stats', params: { id: row.id } })" />
            <el-button link :icon="Edit" aria-label="Sửa" @click="open(row as UserAdmin)" />
            <el-button link :icon="Key" title="Đặt lại mật khẩu" aria-label="Đặt lại mật khẩu" @click="resetPassword(row as UserAdmin)" />
            <el-button link type="danger" :icon="Delete" aria-label="Xóa" :disabled="row.id === auth.user?.id" @click="remove(row as UserAdmin)" />
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="dialog.visible" :title="dialog.editing ? 'Sửa người dùng' : 'Thêm người dùng'" width="min(440px, 92vw)" @closed="formRef?.clearValidate()">
      <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent="save">
        <el-form-item label="Tên đăng nhập" prop="userName">
          <el-input v-model="form.userName" :disabled="!!dialog.editing" />
        </el-form-item>
        <el-form-item label="Tên hiển thị" prop="displayName">
          <el-input v-model="form.displayName" maxlength="100" />
        </el-form-item>
        <el-form-item v-if="!dialog.editing" label="Mật khẩu" prop="password">
          <el-input v-model="form.password" type="password" show-password autocomplete="new-password" />
        </el-form-item>
        <el-form-item label="Vai trò">
          <el-radio-group v-model="form.role">
            <el-radio-button value="User">Học viên</el-radio-button>
            <el-radio-button value="Admin">Quản trị</el-radio-button>
          </el-radio-group>
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
.intro {
  margin-top: 0;
  font-size: 14px;
}

.small {
  font-size: 12px;
}
</style>
