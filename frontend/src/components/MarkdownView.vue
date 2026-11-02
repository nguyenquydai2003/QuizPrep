<script setup lang="ts">
import { computed } from 'vue'
import { renderMarkdown } from '@/utils/markdown'

const props = defineProps<{ source: string | null | undefined }>()

// renderMarkdown escape toàn bộ HTML thô nên dùng v-html an toàn
const html = computed(() => renderMarkdown(props.source ?? ''))
</script>

<template>
  <div class="md" v-html="html" />
</template>
