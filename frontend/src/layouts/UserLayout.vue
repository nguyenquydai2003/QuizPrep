<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowDown, Setting, SwitchButton, User } from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const links = [
  { path: '/', label: 'Tổng quan' },
  { path: '/practice', label: 'Luyện tập' },
  { path: '/exams', label: 'Thi thử' },
  { path: '/review', label: 'Ôn câu sai' },
  { path: '/history', label: 'Lịch sử' },
]

const active = computed(() => {
  if (route.path.startsWith('/attempts')) return '/history'
  return links.find((l) => l.path !== '/' && route.path.startsWith(l.path))?.path ?? '/'
})

function onCommand(command: string) {
  if (command === 'logout') {
    auth.logout()
    router.replace({ name: 'login' })
  } else router.push(command)
}
</script>

<template>
  <div class="user-layout">
    <header class="top">
      <RouterLink to="/" class="brand">QuizPrep</RouterLink>
      <el-menu mode="horizontal" router :default-active="active" class="nav" :ellipsis="true">
        <el-menu-item v-for="l in links" :key="l.path" :index="l.path">{{ l.label }}</el-menu-item>
      </el-menu>
      <el-dropdown trigger="click" @command="onCommand">
        <el-button text class="user-btn">
          <span class="user-name">{{ auth.user?.displayName }}</span>
          <el-icon class="el-icon--right"><ArrowDown /></el-icon>
        </el-button>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item command="/account" :icon="User">Tài khoản</el-dropdown-item>
            <el-dropdown-item v-if="auth.isAdmin" command="/admin" :icon="Setting">Quản trị</el-dropdown-item>
            <el-dropdown-item command="logout" :icon="SwitchButton" divided>Đăng xuất</el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </header>
    <main class="content">
      <RouterView :key="route.path" />
    </main>
  </div>
</template>

<style scoped>
.user-layout {
  min-height: 100%;
}

.top {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  gap: 12px;
  height: 56px;
  padding: 0 16px;
  background: var(--app-surface);
  border-bottom: 1px solid var(--app-border);
}

.brand {
  font-size: 18px;
  font-weight: 700;
  color: var(--el-color-primary);
  text-decoration: none;
  flex-shrink: 0;
}

.nav {
  flex: 1;
  min-width: 0;
  height: 55px;
  border-bottom: none;
}

.user-name {
  max-width: 140px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.content {
  max-width: 1200px;
  margin: 0 auto;
  padding: 20px 16px 40px;
}

@media (max-width: 600px) {
  .user-name {
    max-width: 70px;
  }
}
</style>
