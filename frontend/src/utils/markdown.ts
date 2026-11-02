import MarkdownIt from 'markdown-it'
import hljs from 'highlight.js/lib/core'
import bash from 'highlight.js/lib/languages/bash'
import cpp from 'highlight.js/lib/languages/cpp'
import csharp from 'highlight.js/lib/languages/csharp'
import css from 'highlight.js/lib/languages/css'
import go from 'highlight.js/lib/languages/go'
import java from 'highlight.js/lib/languages/java'
import javascript from 'highlight.js/lib/languages/javascript'
import json from 'highlight.js/lib/languages/json'
import php from 'highlight.js/lib/languages/php'
import python from 'highlight.js/lib/languages/python'
import sql from 'highlight.js/lib/languages/sql'
import typescript from 'highlight.js/lib/languages/typescript'
import xml from 'highlight.js/lib/languages/xml'

hljs.registerLanguage('bash', bash)
hljs.registerLanguage('cpp', cpp)
hljs.registerLanguage('csharp', csharp)
hljs.registerLanguage('css', css)
hljs.registerLanguage('go', go)
hljs.registerLanguage('java', java)
hljs.registerLanguage('javascript', javascript)
hljs.registerLanguage('json', json)
hljs.registerLanguage('php', php)
hljs.registerLanguage('python', python)
hljs.registerLanguage('sql', sql)
hljs.registerLanguage('typescript', typescript)
hljs.registerLanguage('xml', xml)

/** Ngôn ngữ hiển thị trong nút "Chèn khối code" của editor. */
export const codeLanguages = [
  { value: 'csharp', label: 'C#' },
  { value: 'java', label: 'Java' },
  { value: 'javascript', label: 'JavaScript' },
  { value: 'typescript', label: 'TypeScript' },
  { value: 'python', label: 'Python' },
  { value: 'sql', label: 'SQL' },
  { value: 'cpp', label: 'C/C++' },
  { value: 'go', label: 'Go' },
  { value: 'php', label: 'PHP' },
  { value: 'xml', label: 'HTML/XML' },
  { value: 'css', label: 'CSS' },
  { value: 'json', label: 'JSON' },
  { value: 'bash', label: 'Bash' },
]

// html: false => mọi thẻ HTML trong nội dung đều bị escape, nên v-html an toàn
const md: MarkdownIt = new MarkdownIt({
  html: false,
  linkify: true,
  breaks: true,
  highlight(code, lang) {
    const language = lang && hljs.getLanguage(lang) ? lang : null
    const html = language
      ? hljs.highlight(code, { language, ignoreIllegals: true }).value
      : md.utils.escapeHtml(code)
    return `<pre class="hljs"><code>${html}</code></pre>`
  },
})

export function renderMarkdown(source: string): string {
  return md.render(source)
}

export function hasCodeBlock(source: string): boolean {
  return source.includes('```')
}

/** Rút gọn Markdown thành 1 dòng văn bản thuần để hiển thị trong bảng. */
export function toPlainPreview(source: string, max = 140): string {
  // Chỉ bỏ ký hiệu Markdown thật; không xóa ký tự đơn lẻ như # trong "C#" hay _ trong "my_var"
  const text = source
    .replace(/```[\s\S]*?(```|$)/g, ' ')
    .replace(/`([^`]*)`/g, '$1')
    .replace(/^\s*(#{1,6}|>)\s+/gm, '')
    .replace(/\*\*|__|~~/g, '')
    .replace(/\s+/g, ' ')
    .trim()
  return text.length > max ? `${text.slice(0, max)}…` : text
}
