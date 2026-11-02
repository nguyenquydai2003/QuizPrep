<script setup lang="ts">
import { nextTick, onMounted, ref, watch } from 'vue'
import { ArrowDown } from '@element-plus/icons-vue'
import { codeLanguages } from '@/utils/markdown'

const model = defineModel<string>({ required: true })
const props = withDefaults(defineProps<{ rows?: number; placeholder?: string; label?: string }>(), {
  rows: 5,
  placeholder: '',
  label: undefined,
})

const textarea = ref<HTMLTextAreaElement>()

// Tự giãn chiều cao theo nội dung
function autosize() {
  const el = textarea.value
  if (!el) return
  el.style.height = 'auto'
  el.style.height = `${el.scrollHeight + 2}px`
}
watch(model, () => nextTick(autosize))
onMounted(autosize)

async function replaceSelection(build: (selected: string) => { text: string; selectFrom: number; selectTo: number }) {
  const el = textarea.value
  if (!el) return
  const start = el.selectionStart
  const end = el.selectionEnd
  const result = build(model.value.slice(start, end))
  model.value = model.value.slice(0, start) + result.text + model.value.slice(end)
  await nextTick()
  el.focus()
  el.setSelectionRange(start + result.selectFrom, start + result.selectTo)
}

function wrap(mark: string) {
  replaceSelection((sel) => ({ text: `${mark}${sel}${mark}`, selectFrom: mark.length, selectTo: mark.length + sel.length }))
}

function insertCodeBlock(lang: string) {
  replaceSelection((sel) => {
    const open = `\n\`\`\`${lang}\n`
    return { text: `${open}${sel}\n\`\`\`\n`, selectFrom: open.length, selectTo: open.length + sel.length }
  })
}

// Tab chèn 4 dấu cách để gõ code; nhấn Esc rồi Tab để rời ô nhập.
let escaped = false
function onKeydown(ev: KeyboardEvent) {
  if (ev.key === 'Escape') {
    escaped = true
    return
  }
  if (ev.key === 'Tab' && !ev.shiftKey && !escaped) {
    ev.preventDefault()
    replaceSelection(() => ({ text: '    ', selectFrom: 4, selectTo: 4 }))
  }
  escaped = false
}
</script>

<template>
  <div class="md-textarea">
    <div class="toolbar">
      <span v-if="props.label" class="label">{{ props.label }}</span>
      <div class="spacer" />
      <slot name="toolbar" />
      <el-button size="small" text title="In đậm" @click="wrap('**')"><b>B</b></el-button>
      <el-button size="small" text title="Code trong dòng" @click="wrap('`')"><code>`x`</code></el-button>
      <el-dropdown trigger="click" @command="insertCodeBlock">
        <el-button size="small" text>
          Khối code <el-icon class="el-icon--right"><ArrowDown /></el-icon>
        </el-button>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item v-for="lang in codeLanguages" :key="lang.value" :command="lang.value">
              {{ lang.label }}
            </el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </div>
    <textarea
      ref="textarea"
      v-model="model"
      :rows="props.rows"
      :placeholder="props.placeholder"
      :aria-label="props.label"
      spellcheck="false"
      @keydown="onKeydown"
    />
  </div>
</template>

<style scoped>
.md-textarea {
  width: 100%;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  background: var(--el-fill-color-blank);
  transition: border-color 0.2s;
}

.md-textarea:focus-within {
  border-color: var(--el-color-primary);
}

.toolbar {
  display: flex;
  align-items: center;
  gap: 2px;
  padding: 2px 6px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.toolbar .label {
  font-size: 13px;
  color: var(--app-text-muted);
}

.toolbar .spacer {
  flex: 1;
}

.toolbar .el-button + .el-button {
  margin-left: 0;
}

textarea {
  display: block;
  width: 100%;
  padding: 8px 11px;
  border: none;
  outline: none;
  resize: vertical;
  background: transparent;
  color: var(--el-text-color-primary);
  font-family: var(--app-mono);
  font-size: 13.5px;
  line-height: 1.55;
  tab-size: 4;
}
</style>
