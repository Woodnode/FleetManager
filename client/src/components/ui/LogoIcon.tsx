interface LogoIconProps {
  size?: number
}

export default function LogoIcon({ size = 32 }: LogoIconProps) {
  const id = 'an-grad'
  return (
    <svg width={size} height={size} viewBox="0 0 36 36" fill="none" xmlns="http://www.w3.org/2000/svg">
      <defs>
        <linearGradient id={id} x1="4" y1="2" x2="32" y2="34" gradientUnits="userSpaceOnUse">
          <stop stopColor="#4c6ef5" />
          <stop offset="1" stopColor="#6366f1" />
        </linearGradient>
      </defs>

      {/* Hexagonal badge */}
      <path
        d="M18 2 L32 10 L32 26 L18 34 L4 26 L4 10 Z"
        fill={`url(#${id})`}
      />
      {/* Subtle inner border */}
      <path
        d="M18 4.5 L29.5 11 L29.5 25 L18 31.5 L6.5 25 L6.5 11 Z"
        fill="none"
        stroke="rgba(255,255,255,0.15)"
        strokeWidth="1"
      />

      {/* Speedometer arc */}
      <path
        d="M11 24 A7 7 0 0 1 25 24"
        stroke="rgba(255,255,255,0.45)"
        strokeWidth="1.8"
        strokeLinecap="round"
      />
      {/* Active arc (2/3 filled) */}
      <path
        d="M11 24 A7 7 0 0 1 21.5 17.5"
        stroke="white"
        strokeWidth="1.8"
        strokeLinecap="round"
      />

      {/* Tick marks */}
      <path d="M11 24 L12.2 24"   stroke="white" strokeWidth="1.2" strokeLinecap="round" opacity="0.5" />
      <path d="M25 24 L23.8 24"   stroke="white" strokeWidth="1.2" strokeLinecap="round" opacity="0.5" />
      <path d="M18 17 L18 18.2"   stroke="white" strokeWidth="1.2" strokeLinecap="round" opacity="0.5" />

      {/* Needle */}
      <line
        x1="18" y1="24"
        x2="14.2" y2="18.2"
        stroke="white"
        strokeWidth="1.6"
        strokeLinecap="round"
      />
      {/* Center pivot */}
      <circle cx="18" cy="24" r="1.6" fill="white" />
    </svg>
  )
}
