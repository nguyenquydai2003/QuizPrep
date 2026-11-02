<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { attemptsApi } from '@/api/attempts'
import { subjectsApi } from '@/api/subjects'
import type { ReviewSummary, Subject } from '@/api/types'
import { getErrorMessage } from '@/utils/errors'

const router = useRouter()
const summary = ref<ReviewSummary | null>(null)
const subjects = ref<Subject[]>([])
const loading = ref(true)
const starting = ref(false)
const form = reactive({ subjectId: undefined as number | undefined, count: 20 })

const intervals = [
  { box: 1, text: 'Ôn ngay (vừa sai)' },
  { box: 2, text: 'Sau 1 ngày' },
  { box: 3, text: 'Sau 3 ngày' },
  { box: 4, text: 'Sau 7 ngày' },
  { box: 5, text: 'Sau 14 ngày' },
]

onMounted(async () => {
  try {
    ;[summary.value, subjects.value] = await Promise.all([attemptsApi.reviewSummary(), subjectsApi.list()])
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    loading.value = false
  }
})

async function start() {
  starting.value = true
  try {
    const attempt = await attemptsApi.startReview({ subjectId: form.subjectId, count: form.count })
    router.push({ name: 'attempt', params: { id: attempt.id } })
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    starting.value = false
  }
}
</script>

<template>
  <div v-loading="loading" class="narrow">
    <div class="page-header">
      <h1>Ôn câu sai</h1>
    </div>

    <el-card shadow="never" class="section">
      <div class="numbers">
        <div>
          <div class="big">{{ summary?.dueCount ?? 0 }}</div>
          <div class="muted">câu đến hạn ôn</div>
        </div>
        <div>
          <div class="big secondary">{{ summary?.wrongQuestionCount ?? 0 }}</div>
          <div class="muted">câu từng làm sai</div>
        </div>
      </div>

      <el-form v-if="summary?.dueCount" label-position="top" class="form" novalidate @submit.prevent="start">
        <el-form-item label="Môn">
          <el-select v-model="form.subjectId" placeholder="Tất cả môn" clearable class="full">
            <el-option v-for="s in subjects" :key="s.id" :value="s.id" :label="s.name" />
          </el-select>
        </el-form-item>
        <el-form-item label="Số câu tối đa">
          <el-input-number v-model="form.count" :min="1" :max="200" :step="5" />
        </el-form-item>
        <el-button type="primary" size="large" native-type="submit" :loading="starting">Bắt đầu ôn</el-button>
      </el-form>
      <el-alert
        v-else
        type="success"
        :closable="false"
        show-icon
        title="Không có câu nào cần ôn lúc này"
        description="Hãy làm thêm bài luyện tập hoặc thi thử. Câu sai sẽ xuất hiện ở đây."
      />
    </el-card>

    <el-card shadow="never">
      <template #header>Cách hoạt động (lặp lại ngắt quãng)</template>
      <p class="muted how">
        Mỗi câu nằm trong một "hộp". Trả lời sai thì về hộp 1 và cần ôn ngay; trả lời đúng thì lên một hộp và
        khoảng cách ôn lại dài dần. Nhờ vậy bạn dành thời gian cho câu chưa vững, không lặp lại câu đã thuộc.
      </p>
      <ol class="boxes">
        <li v-for="i in intervals" :key="i.box"><strong>Hộp {{ i.box }}</strong> — {{ i.text }}</li>
      </ol>
    </el-card>
  </div>
</template>

<style scoped>
.narrow {
  max-width: 720px;
}

.section {
  margin-bottom: 16px;
}

.numbers {
  display: flex;
  gap: 40px;
  margin-bottom: 16px;
}

.big {
  font-size: 40px;
  font-weight: 700;
  color: var(--el-color-primary);
  line-height: 1.1;
}

.big.secondary {
  color: var(--el-text-color-primary);
}

.form {
  max-width: 360px;
}

.full {
  width: 100%;
}

.how {
  margin-top: 0;
  font-size: 14px;
}

.boxes {
  margin: 0;
  padding-left: 20px;
  font-size: 14px;
  line-height: 1.8;
}
</style>
