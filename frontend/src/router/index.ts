import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

declare module 'vue-router' {
  interface RouteMeta {
    title?: string
    public?: boolean
    requiresAdmin?: boolean
  }
}

const QuestionEditView = () => import('@/views/admin/QuestionEditView.vue')

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { public: true, title: 'Đăng nhập' },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/RegisterView.vue'),
      meta: { public: true, title: 'Đăng ký' },
    },
    {
      // Màn làm bài toàn màn hình, không có menu để tập trung
      path: '/attempts/:id',
      name: 'attempt',
      component: () => import('@/views/AttemptView.vue'),
      props: true,
      meta: { title: 'Làm bài' },
    },
    {
      path: '/',
      component: () => import('@/layouts/UserLayout.vue'),
      children: [
        { path: '', name: 'home', component: () => import('@/views/DashboardView.vue'), meta: { title: 'Tổng quan' } },
        { path: 'practice', name: 'practice', component: () => import('@/views/PracticeView.vue'), meta: { title: 'Luyện tập' } },
        { path: 'exams', name: 'exams', component: () => import('@/views/ExamsView.vue'), meta: { title: 'Thi thử' } },
        { path: 'review', name: 'review', component: () => import('@/views/ReviewView.vue'), meta: { title: 'Ôn câu sai' } },
        { path: 'history', name: 'history', component: () => import('@/views/HistoryView.vue'), meta: { title: 'Lịch sử' } },
        { path: 'account', name: 'account', component: () => import('@/views/AccountView.vue'), meta: { title: 'Tài khoản' } },
        {
          path: 'attempts/:id/result',
          name: 'attempt-result',
          component: () => import('@/views/ResultView.vue'),
          props: true,
          meta: { title: 'Kết quả' },
        },
      ],
    },
    {
      path: '/admin',
      component: () => import('@/layouts/AdminLayout.vue'),
      meta: { requiresAdmin: true },
      children: [
        { path: '', name: 'admin-dashboard', component: () => import('@/views/admin/AdminDashboardView.vue'), meta: { title: 'Tổng quan' } },
        { path: 'subjects', name: 'admin-subjects', component: () => import('@/views/admin/SubjectsView.vue'), meta: { title: 'Môn & chủ đề' } },
        { path: 'questions', name: 'admin-questions', component: () => import('@/views/admin/QuestionsView.vue'), meta: { title: 'Câu hỏi' } },
        { path: 'questions/new', name: 'admin-question-create', component: QuestionEditView, meta: { title: 'Thêm câu hỏi' } },
        {
          path: 'questions/:id(\\d+)',
          name: 'admin-question-edit',
          component: QuestionEditView,
          props: true,
          meta: { title: 'Sửa câu hỏi' },
        },
        { path: 'import', name: 'admin-import', component: () => import('@/views/admin/ImportView.vue'), meta: { title: 'Import câu hỏi' } },
        { path: 'exams', name: 'admin-exams', component: () => import('@/views/admin/ExamTemplatesView.vue'), meta: { title: 'Đề thi' } },
        { path: 'users', name: 'admin-users', component: () => import('@/views/admin/UsersView.vue'), meta: { title: 'Người dùng' } },
        {
          path: 'users/:id/stats',
          name: 'admin-user-stats',
          component: () => import('@/views/admin/UserStatsView.vue'),
          props: true,
          meta: { title: 'Tiến độ học viên' },
        },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (to.meta.public) return auth.isAuthenticated ? '/' : true
  if (!auth.isAuthenticated) return { name: 'login', query: { redirect: to.fullPath } }
  if (to.matched.some((r) => r.meta.requiresAdmin) && !auth.isAdmin) return '/'
  return true
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title} · QuizPrep` : 'QuizPrep'
})

export default router
