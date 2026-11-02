<script setup lang="ts">
import { reactive, ref } from 'vue'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { authApi } from '@/api/auth'
import { useAuthStore } from '@/stores/auth'
import { getErrorMessage } from '@/utils/errors'

const auth = useAuthStore()

const profile = reactive({ displayName: auth.user?.displayName ?? '' })
const savingProfile = ref(false)

async function saveProfile() {
  if (!profile.displayName.trim()) return
  savingProfile.value = true
  try {
    auth.updateUser(await authApi.updateProfile(profile.displayName.trim()))
    ElMessage.success('Đã cập nhật')
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    savingProfile.value = false
  }
}

const pwRef = ref<FormInstance>()
const pw = reactive({ current: '', next: '', confirm: '' })
const pwRules: FormRules = {
  current: [{ required: true, message: 'Nhập mật khẩu hiện tại', trigger: 'blur' }],
  next: [{ required: true, min: 6, message: 'Mật khẩu tối thiểu 6 ký tự', trigger: 'blur' }],
  confirm: [
    {
      validator: (_rule, value, callback) => (value === pw.next ? callback() : callback(new Error('Mật khẩu nhập lại không khớp'))),
      trigger: 'blur',
    },
  ],
}
const savingPw = ref(false)

async function changePassword() {
  if (!(await pwRef.value?.validate().catch(() => false))) return
  savingPw.value = true
  try {
    await authApi.changePassword(pw.current, pw.next)
    ElMessage.success('Đã đổi mật khẩu')
    Object.assign(pw, { current: '', next: '', confirm: '' })
    pwRef.value?.resetFields()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    savingPw.value = false
  }
}
</script>

<template>
  <div class="narrow">
    <div class="page-header">
      <h1>Tài khoản</h1>
    </div>

    <el-card shadow="never" class="section">
      <template #header>Thông tin</template>
      <el-form label-position="top" @submit.prevent="saveProfile">
        <el-form-item label="Tên đăng nhập">
          <el-input :model-value="auth.user?.userName" disabled />
        </el-form-item>
        <el-form-item label="Tên hiển thị">
          <el-input v-model="profile.displayName" maxlength="100" />
        </el-form-item>
        <el-button type="primary" native-type="submit" :loading="savingProfile">Lưu</el-button>
      </el-form>
    </el-card>

    <el-card shadow="never">
      <template #header>Đổi mật khẩu</template>
      <el-form ref="pwRef" :model="pw" :rules="pwRules" label-position="top" @submit.prevent="changePassword">
        <el-form-item label="Mật khẩu hiện tại" prop="current">
          <el-input v-model="pw.current" type="password" show-password autocomplete="current-password" />
        </el-form-item>
        <el-form-item label="Mật khẩu mới" prop="next">
          <el-input v-model="pw.next" type="password" show-password autocomplete="new-password" />
        </el-form-item>
        <el-form-item label="Nhập lại mật khẩu mới" prop="confirm">
          <el-input v-model="pw.confirm" type="password" show-password autocomplete="new-password" />
        </el-form-item>
        <el-button type="primary" native-type="submit" :loading="savingPw">Đổi mật khẩu</el-button>
      </el-form>
    </el-card>
  </div>
</template>

<style scoped>
.narrow {
  max-width: 520px;
}

.section {
  margin-bottom: 16px;
}
</style>
