export default function Spinner({ className = 'w-5 h-5' }: { className?: string }) {
  return (
    <div
      className={`${className} rounded-full animate-spin`}
      style={{ border: '2px solid var(--border-base)', borderTopColor: 'var(--brand-500)' }}
    />
  )
}
