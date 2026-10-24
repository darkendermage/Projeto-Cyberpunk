import type { JSX, SVGProps } from 'react';

/** Line icons for the category tabs, drawn on a 24px grid to match lucide's stroke style. */
const paths: Record<string, JSX.Element> = {
  weapons: (
    <>
      <path d="M3 8h15l2 2v2h-8l-1 2h-3l-1 6H4l1-6-2-2z" />
      <path d="M11 12v2" />
      <path d="M7 8V6h3v2" />
    </>
  ),
  ammo: (
    <>
      <path d="M5 21V10c0-3 1.5-5 2-6 .5 1 2 3 2 6v11z" />
      <path d="M11 21V10c0-3 1.5-5 2-6 .5 1 2 3 2 6v11z" />
      <path d="M17 21V10c0-3 1.5-5 2-6 .5 1 2 3 2 6v11z" />
      <path d="M4 17h18" />
    </>
  ),
  attachment: (
    <>
      <rect x="5" y="9" width="12" height="5" rx="1.5" />
      <path d="M17 10h3v3h-3" />
      <path d="M2 10.5h3v2H2" />
      <path d="M8 14v3M14 14v3" />
      <path d="M10 9V7h2v2" />
    </>
  ),
  vehicleUpgrade: (
    <>
      <circle cx="9" cy="15" r="5" />
      <circle cx="9" cy="15" r="1.5" />
      <path d="m15.5 3.5 2 2-1.5 1.5 1 1 1.5-1.5 2 2-3 3-1.5-.5-3 3" />
    </>
  ),
  gear: (
    <>
      <rect x="4" y="7" width="16" height="13" rx="2" />
      <path d="M9 7V5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" />
      <path d="M4 12h16" />
      <path d="M11 11h2v3h-2z" />
    </>
  ),
  armor: (
    <>
      <path d="M8 3h2a2 2 0 0 0 4 0h2l4 4-2 3v11H6V10L4 7z" />
      <path d="M9 12h6" />
      <path d="M9 16h6" />
    </>
  ),
  cyberware: (
    <>
      <circle cx="12" cy="12" r="3" />
      <path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z" />
      <path d="M12 2v3M12 19v3M19 4l-2 2" />
    </>
  ),
  netrunning: (
    <>
      <rect x="3" y="5" width="18" height="11" rx="1" />
      <path d="M7 9l2 2-2 2M11 13h4" />
      <path d="M8 20h8M12 16v4" />
    </>
  ),
  fashion: (
    <>
      <path d="M12 4a2 2 0 0 1 2 2c0 1.2-2 1.6-2 3" />
      <path d="M12 9 3 16a1 1 0 0 0 .6 1.8h16.8A1 1 0 0 0 21 16z" />
      <path d="M18 3l.6 1.4L20 5l-1.4.6L18 7l-.6-1.4L16 5l1.4-.6z" />
    </>
  ),
  drugs: (
    <>
      <path d="M10.5 20.5 3.5 13.5a5 5 0 0 1 7-7l7 7a5 5 0 0 1-7 7z" />
      <path d="m7 10 7 7" />
      <circle cx="18" cy="6" r="1.5" />
    </>
  ),
  vehicle: (
    <>
      <path d="M3 16v-3l2-5h11l3 4h2v4" />
      <circle cx="7" cy="17" r="2" />
      <circle cx="17" cy="17" r="2" />
      <path d="M9 17h6M7 8l1 4h10" />
    </>
  ),
  hq: (
    <>
      <path d="M4 21V8l8-5 8 5v13" />
      <path d="M9 21v-6h6v6" />
      <path d="M9 10h.01M15 10h.01M12 10h.01" />
    </>
  ),
};

export function CategoryIcon({ icon, size = 24, ...props }: { icon: string; size?: number } & SVGProps<SVGSVGElement>) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5}
      strokeLinecap="round" strokeLinejoin="round" aria-hidden {...props}>
      {paths[icon] ?? paths.gear}
    </svg>
  );
}
