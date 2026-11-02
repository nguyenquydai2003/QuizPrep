<script setup lang="ts">
import { computed } from 'vue'
import { WarningFilled } from '@element-plus/icons-vue'

export interface TopicStat {
  topicId: number
  subjectName: string
  topicName: string
  answered: number
  correct: number
  accuracy: number
}

const props = withDefaults(defineProps<{ topics: TopicStat[]; weakThreshold?: number; clickable?: boolean }>(), {
  weakThreshold: 60,
  clickable: false,
})
const emit = defineEmits<{ select: [topicId: number] }>()

/** Chỗ chừa cho nhãn giá trị ở đầu thanh; trục 0-100% vẽ trên phần còn lại. */
const LABEL_SPACE = 92

// Yếu nhất lên đầu để biết cần ôn gì
const rows = computed(() => [...props.topics].sort((a, b) => a.accuracy - b.accuracy || b.answered - a.answered))
</script>

<template>
  <div class="bars" role="list">
    <div
      v-for="t in rows"
      :key="t.topicId"
      class="row"
      :class="{ clickable }"
      role="listitem"
      :tabindex="clickable ? 0 : undefined"
      :title="`${t.subjectName} / ${t.topicName}: ${t.correct}/${t.answered} câu đúng${clickable ? ' — bấm để luyện chủ đề này' : ''}`"
      @click="clickable && emit('select', t.topicId)"
      @keydown.enter="clickable && emit('select', t.topicId)"
    >
      <div class="name">
        <span class="topic">{{ t.topicName }}</span>
        <span class="subject">{{ t.subjectName }}</span>
      </div>
      <div class="track">
        <div class="bar" :style="{ width: `calc((100% - ${LABEL_SPACE}px) * ${Math.max(t.accuracy, 0.5) / 100})` }" />
        <span class="value">
          {{ t.accuracy }}%
          <span class="count">({{ t.correct }}/{{ t.answered }})</span>
        </span>
      </div>
      <div class="flag">
        <span v-if="t.accuracy < weakThreshold" class="weak">
          <el-icon><WarningFilled /></el-icon> Cần ôn
        </span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.row {
  display: grid;
  grid-template-columns: minmax(120px, 34%) 1fr 78px;
  align-items: center;
  gap: 12px;
  padding: 6px 4px;
  border-radius: 6px;
}

.row.clickable {
  cursor: pointer;
}

.row.clickable:hover,
.row.clickable:focus-visible {
  background: var(--el-fill-color-light);
  outline: none;
}

.name {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.topic {
  font-size: 14px;
  color: var(--viz-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.subject {
  font-size: 12px;
  color: var(--viz-text-muted);
}

.track {
  position: relative;
  display: flex;
  align-items: center;
  gap: 8px;
  height: 24px;
  border-left: 1px solid var(--viz-axis);
  /* Lưới mảnh tại 25/50/75/100% của phần vẽ thanh */
  background: repeating-linear-gradient(to right, transparent 0, transparent calc(25% - 1px), var(--viz-grid) calc(25% - 1px), var(--viz-grid) 25%);
  background-size: calc(100% - 92px) 100%;
  background-repeat: no-repeat;
}

.bar {
  flex-shrink: 0;
  height: 12px;
  background: var(--viz-series-1);
  border-radius: 0 4px 4px 0;
}

.value {
  flex-shrink: 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--viz-text);
  font-variant-numeric: tabular-nums;
}

.count {
  font-weight: 400;
  color: var(--viz-text-muted);
}

.weak {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 12px;
  color: var(--viz-text-secondary);
}

.weak .el-icon {
  color: var(--status-warning);
}

@media (max-width: 600px) {
  .row {
    grid-template-columns: 1fr 70px;
    row-gap: 2px;
  }

  .name {
    grid-column: 1 / -1;
  }
}
</style>
