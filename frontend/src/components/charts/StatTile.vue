<script setup lang="ts">
defineProps<{ label: string; value: string | number; hint?: string }>()
</script>

<template>
  <div class="stat-tile">
    <div class="label">{{ label }}</div>
    <div class="value">{{ value }}</div>
    <div v-if="hint" class="hint">{{ hint }}</div>
    <slot />
  </div>
</template>

<style scoped>
.stat-tile {
  padding: 14px 16px;
  background: var(--viz-surface);
  border: 1px solid var(--app-border);
  border-radius: 10px;
  min-width: 0;
}

.label {
  font-size: 13px;
  color: var(--viz-text-secondary);
}

.value {
  margin-top: 4px;
  font-size: 26px;
  font-weight: 600;
  color: var(--viz-text);
  line-height: 1.2;
}

.hint {
  margin-top: 2px;
  font-size: 12px;
  color: var(--viz-text-muted);
}
</style>
