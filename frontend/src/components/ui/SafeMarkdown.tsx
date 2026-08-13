'use client';

import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import type { JSX, ReactNode } from 'react';
import ReactMarkdown from 'react-markdown';
import type { ExtraProps } from 'react-markdown';
import rehypeHighlight from 'rehype-highlight';
import rehypeSanitize from 'rehype-sanitize';
import remarkGfm from 'remark-gfm';

import { sanitizeSchema } from '@/lib/markdown/sanitizeSchema';

/**
 * ─────────────────────────────────────────────────────────────────────────────
 *  Rendu Markdown STRICT du contenu ASSISTANT — Phase 4.
 * ─────────────────────────────────────────────────────────────────────────────
 *  Pipeline (ordre vérifié sur les sources officielles des paquets) :
 *    remark-gfm (gras, listes, tableaux, liens, code)
 *    → rehype-sanitize (schéma STRICT de @secops-guardian, consommé tel quel)
 *    → rehype-highlight (coloration highlight.js, classes `hljs`/`language-*`
 *      injectées APRÈS l'assainissement — pattern sûr documenté par
 *      rehype-highlight : les classes ne peuvent pas exécuter de script et le
 *      schéma garde `language-*` sur `<code>`).
 *
 *  RÈGLES DE SÉCURITÉ :
 *    - ZÉRO `dangerouslySetInnerHTML` — le Markdown est rendu par
 *      react-markdown (JSX), jamais injecté en HTML brut.
 *    - Consommé UNIQUEMENT pour le contenu assistant (jamais utilisateur,
 *      jamais bulles d'erreur) — voir MessageBubble.
 *    - Liens : `target="_blank" rel="noopener noreferrer nofollow"` posés au
 *      rendu React (le schéma interdit `target`/`rel` dans l'arbre ; le
 *      composant les ajoute APRÈS assainissement).
 *    - Blocs de code : bouton « Copier » (navigator.clipboard, fallback
 *      legacy, micro-animation coche — principe 28).
 * ---------------------------------------------------------------------------
 */

interface SafeMarkdownProps {
  content: string;
}

/**
 * Rendu Markdown du contenu assistant.
 * `content` : texte Markdown provenant du LLM. Le contenu utilisateur
 * n'arrive JAMAIS ici (rendu brut encodé par défaut, voir MessageBubble).
 */
export default function SafeMarkdown({ content }: SafeMarkdownProps) {
  return (
    <div className="markdown-body min-w-0">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[[rehypeSanitize, sanitizeSchema], rehypeHighlight]}
        components={{
          a: Anchor,
          code: Code,
          pre: Pre,
          h1: H1,
          h2: H2,
          h3: H3,
          h4: H4,
          p: Paragraph,
          ul: UnorderedList,
          ol: OrderedList,
          table: Table,
          th: TableHeader,
          td: TableCell,
          blockquote: Blockquote,
          hr: Hr,
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}

/* ────────────────────────── Contexte bloc de code ───────────────────────── */

/** Vrai quand le rendu courant est un `<pre>` (bloc de code) — fourni par
 *  l'override `pre`, consommé par l'override `code` pour distinguer bloc et
 *  inline de façon DÉTERMINISTE (la prop `inline` de react-markdown v9 a été
 *  retirée). */
const IsCodeBlock = createContext(false);

/* ───────────────────────────── Overrides ──────────────────────────────── */

type AnchorProps = JSX.IntrinsicElements['a'] & ExtraProps;

/** Lien : ouverture externe stricte. `href` seul est autorisé par le schéma ;
 *  `target`/`rel` sont posés ici (après assainissement), jamais repris du
 *  contenu LLM. */
function Anchor({ node: _unused, children, ...props }: AnchorProps) {
  void _unused;
  return (
    <a
      {...props}
      target="_blank"
      rel="noopener noreferrer nofollow"
      className="font-medium text-blue-600 underline decoration-blue-300 underline-offset-2 hover:text-blue-700 hover:decoration-blue-500"
    >
      {children}
    </a>
  );
}

type PreProps = JSX.IntrinsicElements['pre'] & ExtraProps;

/** Bloc de code : wrapper relatif + bouton « Copier » en haut à droite.
 *  `pt-9` réserve la place du bouton ; `overflow-x-auto` pour les lignes
 *  longues ; fond GitHub clair `#f6f8fa` adapté aux bulles blanches. */
function Pre({ node: _unused, children, ...props }: PreProps) {
  void _unused;
  const codeText = extractText(children);
  return (
    <div className="relative my-3 first:mt-0 last:mb-0">
      <CopyButton text={codeText} />
      <IsCodeBlock.Provider value={true}>
        <pre
          className="overflow-x-auto rounded-lg border border-slate-200 bg-[#f6f8fa] pt-9 text-[13px] leading-relaxed"
          {...props}
        >
          {children}
        </pre>
      </IsCodeBlock.Provider>
    </div>
  );
}

type CodeProps = JSX.IntrinsicElements['code'] & ExtraProps;

/** `code` : bloc (dans `<pre>`, classes `hljs language-*` conservées) ou
 *  inline (chip gris). La coloration est portée par le thème `github` ; les
 *  classes `hljs-*` sont injectées par rehype-highlight APRÈS sanitize. */
function Code({ node: _unused, className, children, ...props }: CodeProps) {
  void _unused;
  const isBlock = useContext(IsCodeBlock);
  if (isBlock) {
    return (
      <code className={className} {...props}>
        {children}
      </code>
    );
  }
  return (
    <code
      className="rounded bg-slate-100 px-1.5 py-0.5 font-mono text-[0.85em] text-slate-900"
      {...props}
    >
      {children}
    </code>
  );
}

type HeadingProps = JSX.IntrinsicElements['h1'] & ExtraProps;

function H1({ node: _unused, children, ...props }: HeadingProps) {
  void _unused;
  return (
    <h1 className="mb-2 mt-4 text-xl font-bold text-slate-900 first:mt-0" {...props}>
      {children}
    </h1>
  );
}

function H2({ node: _unused, children, ...props }: HeadingProps) {
  void _unused;
  return (
    <h2 className="mb-2 mt-3 text-lg font-bold text-slate-900 first:mt-0" {...props}>
      {children}
    </h2>
  );
}

function H3({ node: _unused, children, ...props }: HeadingProps) {
  void _unused;
  return (
    <h3 className="mb-1.5 mt-3 text-base font-semibold text-slate-900 first:mt-0" {...props}>
      {children}
    </h3>
  );
}

function H4({ node: _unused, children, ...props }: HeadingProps) {
  void _unused;
  return (
    <h4 className="mb-1.5 mt-2 text-sm font-semibold text-slate-900 first:mt-0" {...props}>
      {children}
    </h4>
  );
}

type ParagraphProps = JSX.IntrinsicElements['p'] & ExtraProps;

function Paragraph({ node: _unused, children, ...props }: ParagraphProps) {
  void _unused;
  return (
    <p className="my-1.5 first:mt-0 last:mb-0" {...props}>
      {children}
    </p>
  );
}

type UnorderedListProps = JSX.IntrinsicElements['ul'] & ExtraProps;

function UnorderedList({ node: _unused, children, ...props }: UnorderedListProps) {
  void _unused;
  return (
    <ul className="my-2 list-disc space-y-1 pl-5 first:mt-0 last:mb-0" {...props}>
      {children}
    </ul>
  );
}

type OrderedListProps = JSX.IntrinsicElements['ol'] & ExtraProps;

function OrderedList({ node: _unused, children, ...props }: OrderedListProps) {
  void _unused;
  return (
    <ol className="my-2 list-decimal space-y-1 pl-5 first:mt-0 last:mb-0" {...props}>
      {children}
    </ol>
  );
}

type TableProps = JSX.IntrinsicElements['table'] & ExtraProps;

function Table({ node: _unused, children, ...props }: TableProps) {
  void _unused;
  return (
    <div className="my-3 overflow-x-auto first:mt-0 last:mb-0">
      <table className="w-full border-collapse text-[13px]" {...props}>
        {children}
      </table>
    </div>
  );
}

type TableHeaderProps = JSX.IntrinsicElements['th'] & ExtraProps;

function TableHeader({ node: _unused, children, ...props }: TableHeaderProps) {
  void _unused;
  return (
    <th
      className="border border-slate-300 bg-slate-100 px-2.5 py-1.5 text-left font-semibold"
      {...props}
    >
      {children}
    </th>
  );
}

type TableCellProps = JSX.IntrinsicElements['td'] & ExtraProps;

function TableCell({ node: _unused, children, ...props }: TableCellProps) {
  void _unused;
  return (
    <td className="border border-slate-300 px-2.5 py-1.5 align-top" {...props}>
      {children}
    </td>
  );
}

type BlockquoteProps = JSX.IntrinsicElements['blockquote'] & ExtraProps;

function Blockquote({ node: _unused, children, ...props }: BlockquoteProps) {
  void _unused;
  return (
    <blockquote
      className="my-2 rounded-r-lg border-l-4 border-slate-300 bg-slate-50 py-1 pl-3 pr-2 text-slate-600"
      {...props}
    >
      {children}
    </blockquote>
  );
}

type HrProps = JSX.IntrinsicElements['hr'] & ExtraProps;

function Hr({ node: _unused, ...props }: HrProps) {
  void _unused;
  return <hr className="my-3 border-slate-200" {...props} />;
}

/* ─────────────────────────── Bouton « Copier » ─────────────────────────── */

interface CopyButtonProps {
  text: string;
}

/** Bouton copier du bloc de code : `navigator.clipboard` (fallback legacy),
 *  micro-animation coche pendant 2 s (principe 28). */
function CopyButton({ text }: CopyButtonProps) {
  const [copied, setCopied] = useState(false);
  const timerRef = useRef<number | null>(null);

  useEffect(() => {
    return () => {
      if (timerRef.current !== null) window.clearTimeout(timerRef.current);
    };
  }, []);

  const handleCopy = useCallback(async () => {
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(text);
      } else {
        legacyCopy(text);
      }
    } catch {
      legacyCopy(text);
    }
    setCopied(true);
    if (timerRef.current !== null) window.clearTimeout(timerRef.current);
    timerRef.current = window.setTimeout(() => setCopied(false), 2000);
  }, [text]);

  return (
    <button
      type="button"
      onClick={handleCopy}
      aria-label={copied ? 'Code copié' : 'Copier le code'}
      title={copied ? 'Copié' : 'Copier'}
      className="absolute right-2 top-2 z-10 flex h-7 w-7 items-center justify-center rounded-md border border-slate-200 bg-white text-slate-500 shadow-sm transition-colors hover:bg-slate-100 hover:text-slate-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500"
    >
      {/* key force le remontage → l'animation `copy-pop` rejoue à chaque état. */}
      <span key={copied ? 'check' : 'copy'} className="copy-pop flex">
        {copied ? <CheckIcon /> : <CopyIcon />}
      </span>
    </button>
  );
}

function CopyIcon() {
  return (
    <svg viewBox="0 0 16 16" fill="none" aria-hidden="true" className="h-3.5 w-3.5">
      <rect x="5" y="5" width="8" height="8" rx="1.5" stroke="currentColor" strokeWidth="1.5" />
      <path
        d="M11 5V3.5A1.5 1.5 0 0 0 9.5 2h-6A1.5 1.5 0 0 0 2 3.5v6A1.5 1.5 0 0 0 3.5 11H5"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
      />
    </svg>
  );
}

function CheckIcon() {
  return (
    <svg viewBox="0 0 16 16" fill="none" aria-hidden="true" className="h-3.5 w-3.5 text-emerald-600">
      <path
        d="m3 8.5 3.5 3.5L13 5"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/** Copie de secours pour les contextes non sécurisés (navigator.clipboard
 *  indisponible ou en échec) — zone masquée + execCommand legacy. */
function legacyCopy(text: string): void {
  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.setAttribute('readonly', '');
  textarea.style.position = 'fixed';
  textarea.style.top = '0';
  textarea.style.left = '0';
  textarea.style.opacity = '0';
  document.body.appendChild(textarea);
  textarea.select();
  try {
    document.execCommand('copy');
  } finally {
    document.body.removeChild(textarea);
  }
}

/** Extraction déterministe du texte d'un ReactNode (pour la copie du code). */
function extractText(node: ReactNode): string {
  if (node == null || typeof node === 'boolean') return '';
  if (typeof node === 'string' || typeof node === 'number') return String(node);
  if (Array.isArray(node)) return node.map(extractText).join('');
  const candidate = node as { props?: { children?: ReactNode } };
  if (candidate && typeof candidate === 'object' && candidate.props) {
    return extractText(candidate.props.children);
  }
  return '';
}
