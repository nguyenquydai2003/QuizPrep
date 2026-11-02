<script setup lang="ts">
import { computed } from 'vue'
import { Check, Close } from '@element-plus/icons-vue'
import type { AttemptOption, AttemptQuestion } from '@/api/types'
import MarkdownView from '@/components/MarkdownView.vue'
import { difficultyMeta, optionLetter } from '@/utils/labels'

const props = withDefaults(
  defineProps<{
    question: AttemptQuestion
    selected: number[]
    /** Không cho chọn nữa (đã chấm hoặc đã nộp). */
    locked?: boolean
    total?: number
  }>(),
  { locked: false, total: undefined },
)
const emit = defineEmits<{ toggle: [key: number] }>()

const isMultiple = computed(() => props.question.type === 'MultipleChoice')
const revealed = computed(() => props.question.revealed)

function state(o: AttemptOption) {
  const chosen = props.selected.includes(o.key)
  if (!revealed.value) return chosen ? 'selected' : ''
  if (o.isCorrect) return chosen ? 'correct' : 'missed'
  return chosen ? 'wrong' : 'dim'
}
</script>

<template>
  <div class="question-card">
    <div class="meta">
      <strong>Câu {{ question.order }}<template v-if="total">/{{ total }}</template></strong>
      <span class="muted">{{ question.subjectName }} · {{ question.topicName }}</span>
      <el-tag size="small" :type="difficultyMeta(question.difficulty).tag" effect="plain">
        {{ difficultyMeta(question.difficulty).label }}
      </el-tag>
      <div class="spacer" />
      <slot name="actions" />
    </div>

    <MarkdownView :source="question.content" class="content" />
    <div v-if="isMultiple" class="hint">Chọn <strong>tất cả</strong> đáp án đúng.</div>

    <div class="options" :role="isMultiple ? 'group' : 'radiogroup'">
      <button
        v-for="(o, i) in question.options"
        :key="o.key"
        type="button"
        class="option"
        :class="state(o)"
        :role="isMultiple ? 'checkbox' : 'radio'"
        :aria-checked="selected.includes(o.key)"
        :disabled="locked"
        @click="emit('toggle', o.key)"
      >
        <span class="letter">{{ optionLetter(i) }}</span>
        <MarkdownView :source="o.content" class="option-content" />
        <span v-if="revealed && o.isCorrect" class="badge good">
          <el-icon><Check /></el-icon>{{ selected.includes(o.key) ? 'Đúng' : 'Đáp án đúng' }}
        </span>
        <span v-else-if="revealed && selected.includes(o.key)" class="badge bad">
          <el-icon><Close /></el-icon>Bạn chọn
        </span>
      </button>
    </div>

    <div v-if="revealed" class="result" :class="question.isCorrect ? 'good' : 'bad'">
      <el-icon><component :is="question.isCorrect ? Check : Close" /></el-icon>
      {{ question.isCorrect ? 'Chính xác!' : question.selectedKeys.length ? 'Chưa đúng.' : 'Bạn chưa trả lời câu này.' }}
    </div>
    <div v-if="revealed && question.explanation" class="explanation">
      <div class="explanation-title">Giải thích</div>
      <MarkdownView :source="question.explanation" />
    </div>
  </div>
</template>

<style scoped>
.meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  font-size: 14px;
}

.meta .spacer {
  flex: 1;
}

.content {
  font-size: 16px;
}

.hint {
  margin-top: 8px;
  font-size: 13px;
  color: var(--el-color-primary);
}

.options {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 16px;
}

.option {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  width: 100%;
  padding: 12px 14px;
  text-align: left;
  font: inherit;
  color: inherit;
  background: var(--app-surface);
  border: 1.5px solid var(--app-border);
  border-radius: 10px;
  cursor: pointer;
  transition: border-color 0.15s, background 0.15s;
}

.option:hover:not(:disabled) {
  border-color: var(--el-color-primary-light-5);
  background: var(--el-color-primary-light-9);
}

.option:focus-visible {
  outline: 2px solid var(--el-color-primary);
  outline-offset: 2px;
}

.option:disabled {
  cursor: default;
}

.option.selected {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.option.correct,
.option.missed {
  border-color: var(--app-correct-border);
  background: var(--app-correct-bg);
}

.option.missed {
  border-style: dashed;
}

.option.wrong {
  border-color: var(--el-color-danger-light-5);
  background: var(--el-color-danger-light-9);
}

.option.dim {
  opacity: 0.75;
}

.letter {
  flex-shrink: 0;
  width: 28px;
  height: 28px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  font-weight: 600;
  font-size: 14px;
  background: var(--el-fill-color);
}

.selected .letter {
  background: var(--el-color-primary);
  color: #fff;
}

.correct .letter,
.missed .letter {
  background: var(--status-good);
  color: #fff;
}

.wrong .letter {
  background: var(--status-critical);
  color: #fff;
}

.option-content {
  flex: 1;
  min-width: 0;
  padding-top: 3px;
}

.badge {
  flex-shrink: 0;
  display: inline-flex;
  align-items: center;
  gap: 2px;
  padding-top: 4px;
  font-size: 12px;
  white-space: nowrap;
}

.badge.good {
  color: var(--status-good-text);
}

.badge.bad {
  color: var(--status-critical);
}

.result {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 16px;
  font-weight: 600;
}

.result.good {
  color: var(--status-good-text);
}

.result.bad {
  color: var(--status-critical);
}

.explanation {
  margin-top: 12px;
  padding: 12px 14px;
  border-left: 3px solid var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  border-radius: 4px;
}

.explanation-title {
  font-weight: 600;
  margin-bottom: 4px;
}
</style>
