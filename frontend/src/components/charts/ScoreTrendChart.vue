<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { formatDateTime, formatScore } from '@/utils/labels'

export interface ScorePoint {
  title: string
  submittedAt: string
  score: number
  passScore: number | null
}

const props = defineProps<{ points: ScorePoint[] }>()

const HEIGHT = 240
const PAD = { top: 16, right: 44, bottom: 28, left: 40 }
const Y_TICKS = [0, 25, 50, 75, 100]

// Đo bề rộng thật để chữ không bị co giãn theo viewBox
const root = ref<HTMLDivElement>()
const width = ref(600)
let observer: ResizeObserver | undefined
onMounted(() => {
  observer = new ResizeObserver(([entry]) => (width.value = Math.max(280, entry!.contentRect.width)))
  if (root.value) observer.observe(root.value)
})
onBeforeUnmount(() => observer?.disconnect())

const plotW = computed(() => width.value - PAD.left - PAD.right)
const plotH = HEIGHT - PAD.top - PAD.bottom
const n = computed(() => props.points.length)

const x = (i: number) => PAD.left + (n.value === 1 ? plotW.value / 2 : (i * plotW.value) / (n.value - 1))
const y = (v: number) => PAD.top + plotH * (1 - v / 100)

const linePath = computed(() => props.points.map((p, i) => `${i ? 'L' : 'M'}${x(i)},${y(p.score)}`).join(' '))
const areaPath = computed(() =>
  n.value > 1 ? `${linePath.value} L${x(n.value - 1)},${y(0)} L${x(0)},${y(0)} Z` : '',
)

// Ngưỡng đạt của bài gần nhất
const passScore = computed(() => props.points.at(-1)?.passScore ?? null)

// Nhãn trục X thưa để không chồng lên nhau (~1 nhãn / 90px)
const xLabels = computed(() => {
  const maxLabels = Math.max(2, Math.floor(plotW.value / 90))
  const step = Math.max(1, Math.ceil(n.value / maxLabels))
  return props.points
    .map((p, i) => ({ i, text: new Date(p.submittedAt).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' }) }))
    .filter(({ i }) => i % step === 0 || i === n.value - 1)
})

const hover = ref<number | null>(null)
function onMove(ev: PointerEvent) {
  const rect = (ev.currentTarget as SVGRectElement).getBoundingClientRect()
  const px = ev.clientX - rect.left
  hover.value = n.value === 1 ? 0 : Math.round((px / rect.width) * (n.value - 1))
}
const hovered = computed(() => (hover.value == null ? null : props.points[hover.value]))
const tooltipStyle = computed(() => {
  if (hover.value == null) return {}
  const left = x(hover.value)
  const flip = left > width.value - 200
  return { left: `${flip ? left - 12 : left + 12}px`, top: `${PAD.top}px`, transform: flip ? 'translateX(-100%)' : 'none' }
})

const summary = computed(() => {
  const last = props.points.at(-1)
  return last ? `Điểm ${n.value} bài thi thử gần nhất, bài cuối đạt ${formatScore(last.score)}` : 'Chưa có bài thi'
})
</script>

<template>
  <div ref="root" class="trend">
    <svg :width="width" :height="HEIGHT" role="img" :aria-label="summary">
      <!-- Lưới và trục -->
      <g>
        <template v-for="t in Y_TICKS" :key="t">
          <line :x1="PAD.left" :x2="PAD.left + plotW" :y1="y(t)" :y2="y(t)" :stroke="t === 0 ? 'var(--viz-axis)' : 'var(--viz-grid)'" stroke-width="1" />
          <text :x="PAD.left - 8" :y="y(t)" dy="0.32em" text-anchor="end" class="tick">{{ t }}%</text>
        </template>
        <text v-for="l in xLabels" :key="l.i" :x="x(l.i)" :y="HEIGHT - 8" text-anchor="middle" class="tick">{{ l.text }}</text>
      </g>

      <!-- Ngưỡng đạt -->
      <g v-if="passScore != null">
        <line :x1="PAD.left" :x2="PAD.left + plotW" :y1="y(passScore)" :y2="y(passScore)" stroke="var(--viz-text-muted)" stroke-width="1" />
        <text :x="PAD.left + plotW + 4" :y="y(passScore)" dy="0.32em" class="tick">Đạt {{ passScore }}%</text>
      </g>

      <!-- Dữ liệu -->
      <path v-if="areaPath" :d="areaPath" fill="var(--viz-series-1)" fill-opacity="0.1" />
      <path :d="linePath" fill="none" stroke="var(--viz-series-1)" stroke-width="2" stroke-linejoin="round" stroke-linecap="round" />
      <line v-if="hover != null" :x1="x(hover)" :x2="x(hover)" :y1="PAD.top" :y2="y(0)" stroke="var(--viz-axis)" stroke-width="1" />
      <circle
        v-for="(p, i) in points"
        :key="i"
        :cx="x(i)"
        :cy="y(p.score)"
        :r="hover === i ? 6 : 4"
        fill="var(--viz-series-1)"
        stroke="var(--viz-surface)"
        stroke-width="2"
      />
      <!-- Nhãn giá trị ở cuối đường -->
      <text v-if="n > 0 && hover == null" :x="x(n - 1) + 8" :y="y(points[n - 1]!.score) - 10" class="end-label">
        {{ formatScore(points[n - 1]!.score) }}
      </text>

      <rect
        :x="PAD.left - 10"
        :y="PAD.top"
        :width="plotW + 20"
        :height="plotH"
        fill="transparent"
        @pointermove="onMove"
        @pointerleave="hover = null"
      />
    </svg>

    <div v-if="hovered" class="tooltip" :style="tooltipStyle">
      <div class="tt-title">{{ hovered.title }}</div>
      <div class="tt-muted">{{ formatDateTime(hovered.submittedAt) }}</div>
      <div class="tt-value">
        <span class="swatch" />
        {{ formatScore(hovered.score) }}
        <template v-if="hovered.passScore != null">
          <span :class="hovered.score >= hovered.passScore ? 'pass' : 'fail'">
            {{ hovered.score >= hovered.passScore ? '✓ Đạt' : '✗ Chưa đạt' }}
          </span>
        </template>
      </div>
    </div>

    <details class="table-view">
      <summary>Xem dạng bảng</summary>
      <table>
        <thead>
          <tr><th>Bài thi</th><th>Ngày nộp</th><th>Điểm</th></tr>
        </thead>
        <tbody>
          <tr v-for="(p, i) in points" :key="i">
            <td>{{ p.title }}</td>
            <td>{{ formatDateTime(p.submittedAt) }}</td>
            <td class="num">{{ formatScore(p.score) }}</td>
          </tr>
        </tbody>
      </table>
    </details>
  </div>
</template>

<style scoped>
.trend {
  position: relative;
  width: 100%;
  overflow: hidden;
}

svg {
  display: block;
  font-family: inherit;
}

.tick {
  font-size: 11px;
  fill: var(--viz-text-muted);
  font-variant-numeric: tabular-nums;
}

.end-label {
  font-size: 12px;
  font-weight: 600;
  fill: var(--viz-text);
}

.tooltip {
  position: absolute;
  pointer-events: none;
  min-width: 160px;
  max-width: 240px;
  padding: 8px 10px;
  background: var(--viz-surface);
  border: 1px solid var(--app-border);
  border-radius: 8px;
  box-shadow: 0 4px 16px rgb(0 0 0 / 8%);
  font-size: 12px;
  color: var(--viz-text);
}

.tt-title {
  font-weight: 600;
}

.tt-muted {
  color: var(--viz-text-muted);
}

.tt-value {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 4px;
  font-size: 14px;
  font-weight: 600;
}

.swatch {
  width: 10px;
  height: 2px;
  background: var(--viz-series-1);
}

.pass {
  color: var(--status-good-text);
  font-size: 12px;
}

.fail {
  color: var(--status-critical);
  font-size: 12px;
}

.table-view {
  margin-top: 8px;
  font-size: 13px;
}

.table-view summary {
  cursor: pointer;
  color: var(--viz-text-secondary);
}

.table-view table {
  width: 100%;
  margin-top: 8px;
  border-collapse: collapse;
}

.table-view th,
.table-view td {
  text-align: left;
  padding: 4px 8px;
  border-bottom: 1px solid var(--viz-grid);
}

.num {
  font-variant-numeric: tabular-nums;
}
</style>
