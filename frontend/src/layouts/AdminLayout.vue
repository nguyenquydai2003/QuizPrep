<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Collection,
  DataAnalysis,
  Document,
  EditPen,
  Expand,
  Fold,
  SwitchButton,
  Tickets,
  Upload,
  User as UserIcon,
} from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const collapsed = ref(window.innerWidth < 768)
const activeMenu = computed(() => {
  for (const section of ['/admin/questions', '/admin/users']) if (route.path.startsWith(section)) return section
  return route.path
})

function logout() {
  auth.logout()
  router.replace({ name: 'login' })
}
</script>

<template>
  <el-container class="admin-layout">
    <el-aside :width="collapsed ? '64px' : '220px'" class="aside">
      <div class="brand">{{ collapsed ? 'QP' : 'QuizPrep Admin' }}</div>
      <el-menu router :default-active="activeMenu" :collapse="collapsed" class="menu">
        <el-menu-item index="/admin">
          <el-icon><DataAnalysis /></el-icon>
          <template #title>Tổng quan</template>
        </el-menu-item>
        <el-menu-item index="/admin/subjects">
          <el-icon><Collection /></el-icon>
          <template #title>Môn & chủ đề</template>
        </el-menu-item>
        <el-menu-item index="/admin/questions">
          <el-icon><Document /></el-icon>
          <template #title>Câu hỏi</template>
        </el-menu-item>
        <el-menu-item index="/admin/import">
          <el-icon><Upload /></el-icon>
          <template #title>Import câu hỏi</template>
        </el-menu-item>
        <el-menu-item index="/admin/exams">
          <el-icon><Tickets /></el-icon>
          <template #title>Đề thi</template>
        </el-menu-item>
        <el-menu-item index="/admin/users">
          <el-icon><UserIcon /></el-icon>
          <template #title>Người dùng</template>
        </el-menu-item>
        <el-menu-item index="/">
          <el-icon><EditPen /></el-icon>
          <template #title>Trang làm bài</template>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <el-container class="body">
      <el-header class="header">
        <el-button text :icon="collapsed ? Expand : Fold" aria-label="Thu gọn menu" @click="collapsed = !collapsed" />
        <div class="spacer" />
        <span class="user">{{ auth.user?.displayName }}</span>
        <el-button text :icon="SwitchButton" @click="logout">Đăng xuất</el-button>
      </el-header>
      <el-main class="main">
        <!-- key theo path (không gồm query) để /questions/new ↔ /questions/:id mount lại form -->
        <RouterView :key="route.path" />
      </el-main>
    </el-container>
  </el-container>
</template>

<style scoped>
.admin-layout {
  height: 100%;
}

.aside {
  background: var(--app-surface);
  border-right: 1px solid var(--app-border);
  transition: width 0.2s;
  overflow-x: hidden;
}

.brand {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 16px;
  color: var(--el-color-primary);
  border-bottom: 1px solid var(--app-border);
  white-space: nowrap;
}

.menu {
  border-right: none;
}

.body {
  min-width: 0;
}

.header {
  height: 56px;
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--app-surface);
  border-bottom: 1px solid var(--app-border);
}

.header .spacer {
  flex: 1;
}

.user {
  font-size: 14px;
  color: var(--app-text-muted);
}

.main {
  padding: 20px;
}

@media (max-width: 600px) {
  .main {
    padding: 12px;
  }

  .user {
    display: none;
  }
}
</style>
