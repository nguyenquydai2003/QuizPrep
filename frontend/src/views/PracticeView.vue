<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { attemptsApi } from '@/api/attempts'
import { subjectsApi } from '@/api/subjects'
import type { Difficulty, Subject } from '@/api/types'
import { getErrorMessage } from '@/utils/errors'
import { difficultyOptions, subjectCascaderOptions } from '@/utils/labels'

const router = useRouter()
const subjects = ref<Subject[]>([])
const starting = ref(false)

const form = reactive({
  location: [] as number[],
  difficulty: '' as Difficulty | '',
  count: 20,
  onlyUnseen: false,
})

const options = computed(() => subjectCascaderOptions(subjects.value))
const totalQuestions = computed(() => subjects.value.reduce((n, s) => n + s.questionCount, 0))

onMounted(async () => {
  try {
    subjects.value = await subjectsApi.list()
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  }
})

async function start() {
  starting.value = true
  try {
    const attempt = await attemptsApi.startPractice({
      subjectId: form.location[0],
      topicId: form.location[1],
      difficulty: form.difficulty || undefined,
      count: form.count,
      onlyUnseen: form.onlyUnseen,
    })
    router.push({ name: 'attempt', params: { id: attempt.id } })
  } catch (e) {
    ElMessage.error(getErrorMessage(e))
  } finally {
    starting.value = false
  }
}
</script>

<template>
  <div class="narrow">
    <div class="page-header">
      <h1>Luyện tập</h1>
    </div>
    <el-card shadow="never">
      <p class="muted intro">
        Trả lời từng câu và xem ngay đáp án, lời giải thích. Câu trả lời sai sẽ tự động vào danh sách
        <RouterLink to="/review">Ôn câu sai</RouterLink>.
      </p>
      <!-- novalidate: input number bước 5 làm trình duyệt chặn submit với giá trị lẻ -->
      <el-form label-position="top" novalidate @submit.prevent="start">
        <el-form-item label="Phạm vi">
          <el-cascader
            v-model="form.location"
            :options="options"
            :props="{ checkStrictly: true }"
            placeholder="Tất cả môn"
            clearable
            filterable
            class="full"
          />
        </el-form-item>
        <el-form-item label="Độ khó">
          <el-radio-group v-model="form.difficulty">
            <el-radio-button value="">Tất cả</el-radio-button>
            <el-radio-button v-for="d in difficultyOptions" :key="d.value" :value="d.value">{{ d.label }}</el-radio-button>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="Số câu">
          <el-input-number v-model="form.count" :min="1" :max="200" :step="5" />
        </el-form-item>
        <el-form-item>
          <el-switch v-model="form.onlyUnseen" active-text="Chỉ câu chưa làm bao giờ" />
        </el-form-item>
        <el-button type="primary" size="large" native-type="submit" :loading="starting" :disabled="totalQuestions === 0">
          Bắt đầu luyện tập
        </el-button>
        <span v-if="totalQuestions === 0" class="muted empty-hint">Ngân hàng chưa có câu hỏi nào.</span>
      </el-form>
    </el-card>
  </div>
</template>

<style scoped>
.narrow {
  max-width: 640px;
}

.intro {
  margin-top: 0;
}

.full {
  width: 100%;
}

.empty-hint {
  margin-left: 12px;
  font-size: 13px;
}
</style>
