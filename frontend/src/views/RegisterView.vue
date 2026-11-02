<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { authApi } from '@/api/auth'
import { useAuthStore } from '@/stores/auth'
import { getErrorMessage } from '@/utils/errors'

const auth = useAuthStore()
const router = useRouter()
const allowed = ref<boolean | null>(null)

onMounted(async () => {
  try {
    allowed.value = (await authApi.options()).allowRegistration
  } catch {
    allowed.value = false
  }
})

const formRef = ref<FormInstance>()
const form = reactive({ userName: '', displayName: '', password: '', confirm: '' })
const rules: FormRules = {
  userName: [
    { required: true, message: 'Nhập tên đăng nhập', trigger: 'blur' },
    { pattern: /^[a-zA-Z0-9._-]{3,50}$/, message: '3-50 ký tự: chữ không dấu, số, . _ -', trigger: 'blur' },
  ],
  displayName: [{ required: true, message: 'Nhập tên hiển thị', trigger: 'blur' }],
  password: [{ required: true, min: 6, message: 'Mật khẩu tối thiểu 6 ký tự', trigger: 'blur' }],
  confirm: [
    {
      validator: (_rule, value, callback) => (value === form.password ? callback() : callback(new Error('Mật khẩu nhập lại không khớp'))),
      trigger: 'blur',
    },
  ],
}
const loading = ref(false)

async function submit() {
  if (!(await formRef.value?.validate().catch(() => false))) return
  loading.value = true
  try {
    await auth.register({ userName: form.userName, displayName: form.displayName, password: form.password })
    router.replace('/')
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <el-card class="auth-card" shadow="always">
      <h1>Tạo tài khoản</h1>
      <el-alert
        v-if="allowed === false"
        type="info"
        :closable="false"
        show-icon
        title="Chức năng đăng ký đang tắt"
        description="Hãy liên hệ quản trị viên để được cấp tài khoản."
      />
      <el-form v-else ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent="submit">
        <el-form-item label="Tên đăng nhập" prop="userName">
          <el-input v-model="form.userName" autocomplete="username" />
        </el-form-item>
        <el-form-item label="Tên hiển thị" prop="displayName">
          <el-input v-model="form.displayName" maxlength="100" />
        </el-form-item>
        <el-form-item label="Mật khẩu" prop="password">
          <el-input v-model="form.password" type="password" show-password autocomplete="new-password" />
        </el-form-item>
        <el-form-item label="Nhập lại mật khẩu" prop="confirm">
          <el-input v-model="form.confirm" type="password" show-password autocomplete="new-password" />
        </el-form-item>
        <el-button type="primary" native-type="submit" :loading="loading" class="submit">Đăng ký</el-button>
      </el-form>
      <p class="switch">Đã có tài khoản? <RouterLink to="/login">Đăng nhập</RouterLink></p>
    </el-card>
  </div>
</template>

<style scoped>
.auth-page {
  min-height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  background: linear-gradient(135deg, var(--el-color-primary-light-9), var(--app-bg));
}

.auth-card {
  width: 100%;
  max-width: 400px;
}

h1 {
  margin: 0 0 16px;
  font-size: 22px;
}

.submit {
  width: 100%;
}

.switch {
  margin: 16px 0 0;
  font-size: 14px;
  text-align: center;
}
</style>
