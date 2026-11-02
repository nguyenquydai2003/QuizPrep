<script setup lang="ts">
import { computed } from 'vue'
import type { StatsOverview } from '@/api/types'
import ActivityHeatmap from '@/components/charts/ActivityHeatmap.vue'
import ScoreTrendChart from '@/components/charts/ScoreTrendChart.vue'
import StatTile from '@/components/charts/StatTile.vue'
import TopicAccuracyBars from '@/components/charts/TopicAccuracyBars.vue'
import { formatDuration, formatScore } from '@/utils/labels'
import { toPlainPreview } from '@/utils/markdown'

const props = withDefaults(defineProps<{ stats: StatsOverview; interactive?: boolean }>(), { interactive: true })
const emit = defineEmits<{ practiceTopic: [topicId: number] }>()

const tiles = computed(() => {
  const s = props.stats
  return [
    { label: 'Độ chính xác', value: formatScore(s.accuracyPercent), hint: `${s.totalCorrect}/${s.totalAnswered} câu đúng` },
    { label: 'Độ phủ ngân hàng', value: formatScore(s.coveragePercent), hint: `${s.distinctAnswered}/${s.questionBankSize} câu đã làm` },
    { label: 'Chuỗi ngày học', value: `${s.currentStreak} ngày`, hint: `Dài nhất ${s.longestStreak} ngày` },
    { label: 'Thi thử đạt', value: `${s.examsPassed}/${s.examsTaken}`, hint: s.bestExamScore != null ? `Cao nhất ${formatScore(s.bestExamScore)}` : 'Chưa thi' },
    { label: 'Câu cần ôn', value: s.dueReviewCount, hint: 'Đến hạn ôn lại hôm nay' },
    { label: 'Thời gian / câu', value: formatDuration(s.avgSecondsPerQuestion), hint: 'Trung bình' },
  ]
})
</script>

<template>
  <div class="stats-dashboard">
    <div class="tiles">
      <StatTile v-for="t in tiles" :key="t.label" :label="t.label" :value="t.value" :hint="t.hint" />
    </div>

    <div class="grid">
      <el-card shadow="never" class="wide">
        <template #header>Điểm thi thử</template>
        <ScoreTrendChart v-if="stats.scoreTrend.length" :points="stats.scoreTrend" />
        <el-empty v-else :image-size="60" description="Chưa có bài thi thử nào" />
      </el-card>

      <el-card shadow="never">
        <template #header>
          <div class="card-header">
            <span>Độ chính xác theo chủ đề</span>
            <span v-if="interactive && stats.topics.length" class="muted small">Bấm vào chủ đề để luyện</span>
          </div>
        </template>
        <TopicAccuracyBars
          v-if="stats.topics.length"
          :topics="stats.topics"
          :clickable="interactive"
          @select="(id) => emit('practiceTopic', id)"
        />
        <el-empty v-else :image-size="60" description="Làm bài để xem điểm mạnh, yếu" />
      </el-card>

      <el-card shadow="never">
        <template #header>Hoạt động 26 tuần gần đây</template>
        <ActivityHeatmap :activity="stats.activity" />
      </el-card>

      <el-card shadow="never" class="wide">
        <template #header>Câu hay sai nhất</template>
        <el-table v-if="stats.topWrong.length" :data="stats.topWrong" size="small">
          <el-table-column label="Câu hỏi" min-width="260">
            <template #default="{ row }">{{ toPlainPreview(row.contentPreview, 120) }}</template>
          </el-table-column>
          <el-table-column prop="topicName" label="Chủ đề" min-width="120" />
          <el-table-column label="Sai / đã làm" width="110" align="right">
            <template #default="{ row }">
              <span class="num">{{ row.wrongCount }} / {{ row.answeredCount }}</span>
            </template>
          </el-table-column>
        </el-table>
        <el-empty v-else :image-size="60" description="Chưa có câu sai nào" />
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.tiles {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 12px;
  margin-bottom: 16px;
}

.grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.wide {
  grid-column: 1 / -1;
}

.card-header {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 8px;
}

.small {
  font-size: 12px;
}

.num {
  font-variant-numeric: tabular-nums;
}

@media (max-width: 900px) {
  .grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
