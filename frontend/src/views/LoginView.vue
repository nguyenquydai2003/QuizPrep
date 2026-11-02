<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { Lock, User } from '@element-plus/icons-vue'
import { authApi } from '@/api/auth'
import { useAuthStore } from '@/stores/auth'
import { getErrorMessage } from '@/utils/errors'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const formRef = ref<FormInstance>()
const form = reactive({ userName: '', password: '' })
const rules: FormRules = {
  userName: [{ required: true, message: 'Nhập tên đăng nhập', trigger: 'blur' }],
  password: [{ required: true, message: 'Nhập mật khẩu', trigger: 'blur' }],
}
const loading = ref(false)
const allowRegistration = ref(false)

onMounted(async () => {
  try {
    allowRegistration.value = (await authApi.options()).allowRegistration
  } catch {
    // Không lấy được cấu hình thì ẩn link đăng ký
  }
})

async function submit() {
  if (!(await formRef.value?.validate().catch(() => false))) return
  loading.value = true
  try {
    await auth.login(form.userName, form.password)
    const redirect = route.query.redirect
    await router.replace(typeof redirect === 'string' && redirect.startsWith('/') ? redirect : '/')
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <el-card class="login-card" shadow="always">
      <h1>QuizPrep</h1>
      <p class="muted">Luyện thi trắc nghiệm tuyển dụng</p>
      <el-form ref="formRef" :model="form" :rules="rules" size="large" @submit.prevent="submit">
        <el-form-item prop="userName">
          <el-input v-model="form.userName" placeholder="Tên đăng nhập" :prefix-icon="User" autocomplete="username" />
        </el-form-item>
        <el-form-item prop="password">
          <el-input
            v-model="form.password"
            type="password"
            placeholder="Mật khẩu"
            :prefix-icon="Lock"
            show-password
            autocomplete="current-password"
          />
        </el-form-item>
        <el-button type="primary" native-type="submit" :loading="loading" class="submit">Đăng nhập</el-button>
      </el-form>
      <p v-if="allowRegistration" class="switch">Chưa có tài khoản? <RouterLink to="/register">Đăng ký</RouterLink></p>
    </el-card>
  </div>
</template>

<style scoped>
.login-page {
  min-height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  background: linear-gradient(135deg, var(--el-color-primary-light-9), var(--app-bg));
}

.login-card {
  width: 100%;
  max-width: 380px;
  padding: 8px 8px 4px;
}

h1 {
  margin: 0;
  font-size: 26px;
  color: var(--el-color-primary);
}

p {
  margin: 4px 0 24px;
}

.submit {
  width: 100%;
}

.switch {
  margin: 16px 0 4px;
  font-size: 14px;
  text-align: center;
}
</style>
