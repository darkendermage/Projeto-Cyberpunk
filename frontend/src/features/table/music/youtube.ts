/** The bits of the YouTube IFrame Player API the table uses. */
export interface YtPlayer {
  playVideo(): void;
  pauseVideo(): void;
  seekTo(seconds: number, allowSeekAhead: boolean): void;
  getCurrentTime(): number;
  getPlaylistIndex(): number;
  setVolume(volume: number): void;
  mute(): void;
  unMute(): void;
  loadVideoById(id: string, startSeconds?: number): void;
  cueVideoById(id: string, startSeconds?: number): void;
  cuePlaylist(args: { list: string; listType: 'playlist'; index?: number; startSeconds?: number }): void;
  loadPlaylist(args: { list: string; listType: 'playlist'; index?: number; startSeconds?: number }): void;
  destroy(): void;
}

interface YtNamespace {
  Player: new (el: HTMLElement, options: {
    width?: string | number; height?: string | number; videoId?: string;
    playerVars?: Record<string, string | number>;
    events?: { onReady?: () => void; onError?: (e: { data: number }) => void; onStateChange?: (e: { data: number }) => void };
  }) => YtPlayer;
}

declare global {
  interface Window { YT?: YtNamespace; onYouTubeIframeAPIReady?: () => void }
}

let loading: Promise<YtNamespace> | null = null;

/** Loads the IFrame Player API once, only when the table has music. */
export function loadYouTube(): Promise<YtNamespace> {
  if (window.YT?.Player) return Promise.resolve(window.YT);
  loading ??= new Promise((resolve, reject) => {
    const previous = window.onYouTubeIframeAPIReady;
    window.onYouTubeIframeAPIReady = () => { previous?.(); if (window.YT) resolve(window.YT); };
    const script = document.createElement('script');
    script.src = 'https://www.youtube.com/iframe_api';
    script.async = true;
    script.onerror = () => { loading = null; reject(new Error('YouTube API unavailable')); };
    document.head.appendChild(script);
  });
  return loading;
}

/** YouTube errors 101 and 150: the owner doesn't allow playing the video outside YouTube. */
export const isEmbedBlocked = (code: number) => code === 101 || code === 150;
