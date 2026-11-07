import { Link } from 'react-router'

export function Logo({ withText = true }: { withText?: boolean }) {
  return (
    <Link to="/" className="inline-flex min-h-11 items-center gap-2" aria-label="StajDesk ana sayfa">
      <svg width="32" height="32" viewBox="0 0 32 32" aria-hidden="true">
        <defs>
          <linearGradient id="logo-g" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0" stopColor="#a78bfa" />
            <stop offset="1" stopColor="#7c3aed" />
          </linearGradient>
        </defs>
        <rect width="32" height="32" rx="8" fill="url(#logo-g)" />
        <path d="M9 11h14v3H9zM9 16.5h10v3H9zM9 22h6v3H9z" fill="#fff" />
      </svg>
      {withText && <span className="text-lg font-bold text-gray-800 dark:text-gray-100">StajDesk</span>}
    </Link>
  )
}
