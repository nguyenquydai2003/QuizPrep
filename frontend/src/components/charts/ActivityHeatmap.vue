<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'

const props = withDefaults(defineProps<{ activity: { date: string; count: number }[]; weeks?: number }>(), { weeks: 26 })

const CELL = 12
const GAP = 2
const LEFT = 26
const TOP = 16
const WEEKDAYS = ['T2', '', 'T4', '', 'T6', '', 'CN']

const pad = (n: number) => String(n).padStart(2, '0')
const toKey = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`

const counts = computed(() => new Map(props.activity.map((a) => [a.date, a.count])))
const max = computed(() => Math.max(1, ...props.activity.map((a) => a.count)))

// Lưới theo tuần (cột), thứ Hai ở trên cùng, kết thúc ở tuần hiện tại
const cells = computed(() => {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const mondayOffset = (today.getDay() + 6) % 7
  const start = new Date(today)
  start.setDate(today.getDate() - mondayOffset - (props.weeks - 1) * 7)

  const result: { key: string; date: Date; col: number; row: number; count: number; level: number }[] = []
  for (let col = 0; col < props.weeks; col++) {
    for (let row = 0; row < 7; row++) {
      const date = new Date(start)
      date.setDate(start.getDate() + col * 7 + row)
      if (date > today) continue
      const key = toKey(date)
      const count = counts.value.get(key) ?? 0
      result.push({ key, date, col, row, count, level: count === 0 ? 0 : Math.min(4, Math.ceil((count / max.value) * 4)) })
    }
  }
  return result
})

const months = computed(() => {
  const labels: { col: number; text: string }[] = []
  let last = -1
  for (const c of cells.value) {
    if (c.row !== 0) continue
    const m = c.date.getMonth()
    if (m !== last) {
      if (c.col < props.weeks - 1) labels.push({ col: c.col, text: `Th${m + 1}` })
      last = m
    }
  }
  return labels
})

const width = LEFT + props.weeks * (CELL + GAP)
const height = TOP + 7 * (CELL + GAP)
const fill = (level: number) => (level === 0 ? 'var(--viz-empty)' : `var(--viz-seq-${level})`)

const hovered = ref<(typeof cells.value)[number] | null>(null)
const tooltipStyle = computed(() => {
  const c = hovered.value
  if (!c) return {}
  return { left: `${LEFT + c.col * (CELL + GAP) + CELL / 2}px`, top: `${TOP + c.row * (CELL + GAP)}px` }
})
const describe = (c: { date: Date; count: number }) =>
  `${c.count} câu · ${c.date.toLocaleDateString('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' })}`

const total = computed(() => props.activity.reduce((s, a) => s + a.count, 0))

// Màn hình hẹp: cuộn sẵn về tuần gần nhất
const scroller = ref<HTMLDivElement>()
onMounted(() => nextTick(() => scroller.value && (scroller.value.scrollLeft = scroller.value.scrollWidth)))
</script>

<template>
  <div class="heatmap">
    <div ref="scroller" class="scroller">
      <div class="canvas" :style="{ width: `${width}px` }">
        <svg :width="width" :height="height" role="img" :aria-label="`${total} câu trong ${weeks} tuần gần đây`">
          <text v-for="m in months" :key="m.col" :x="LEFT + m.col * (CELL + GAP)" y="10" class="label">{{ m.text }}</text>
          <text v-for="(d, i) in WEEKDAYS" :key="i" x="0" :y="TOP + i * (CELL + GAP) + CELL - 2" class="label">{{ d }}</text>
          <rect
            v-for="c in cells"
            :key="c.key"
            :x="LEFT + c.col * (CELL + GAP)"
            :y="TOP + c.row * (CELL + GAP)"
            :width="CELL"
            :height="CELL"
            rx="2"
            :fill="fill(c.level)"
            @pointerenter="hovered = c"
            @pointerleave="hovered = null"
          />
        </svg>
        <div v-if="hovered" class="tooltip" :style="tooltipStyle">{{ describe(hovered) }}</div>
      </div>
    </div>
    <div class="legend">
      <span>Ít</span>
      <span v-for="l in [0, 1, 2, 3, 4]" :key="l" class="swatch" :style="{ background: fill(l) }" />
      <span>Nhiều</span>
    </div>
  </div>
</template>

<style scoped>
.scroller {
  overflow-x: auto;
  padding-top: 4px;
}

.canvas {
  position: relative;
}

svg {
  display: block;
}

.label {
  font-size: 10px;
  fill: var(--viz-text-muted);
}

.tooltip {
  position: absolute;
  transform: translate(-50%, calc(-100% - 6px));
  pointer-events: none;
  white-space: nowrap;
  padding: 4px 8px;
  background: var(--viz-surface);
  border: 1px solid var(--app-border);
  border-radius: 6px;
  box-shadow: 0 4px 12px rgb(0 0 0 / 8%);
  font-size: 12px;
  color: var(--viz-text);
}

.legend {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 4px;
  margin-top: 6px;
  font-size: 11px;
  color: var(--viz-text-muted);
}

.swatch {
  width: 12px;
  height: 12px;
  border-radius: 2px;
}
</style>
