<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import { statsApi } from '@/api/admin'
import type { StatsOverview } from '@/api/types'
import StatsDashboard from '@/components/StatsDashboard.vue'
import { getErrorMessage } from '@/utils/errors'

const props = defineProps<{ id: string }>()
const stats = ref<StatsOverview | null>(null)
const loading = ref(true)

onMounted(async () => {
  try {
    stats.value = await statsApi.overview(props.id)
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div v-loading="loading">
    <div class="page-header">
      <el-button :icon="ArrowLeft" text @click="$router.push({ name: 'admin-users' })">Người dùng</el-button>
      <h1>Tiến độ: {{ stats?.displayName }}</h1>
    </div>
    <StatsDashboard v-if="stats" :stats="stats" :interactive="false" />
  </div>
</template>
