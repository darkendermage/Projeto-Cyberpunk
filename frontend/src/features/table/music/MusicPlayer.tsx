import { useEffect, useRef, useState } from 'react';
import { Music, Volume2, VolumeX, X } from 'lucide-react';
import type { TableMusic } from '../../../api/tableTypes';
import { Button, IconButton } from '../../../components/ui/ui';
import { t } from '../../../i18n';
import type { TablePrefs } from '../tablePrefs';
import { expectedPosition, needsSeek } from './sync';
import { isEmbedBlocked, loadYouTube, type YtPlayer } from './youtube';
import styles from '../Table.module.css';

const CHECK_MS = 3000;

/** Whether the player is on the table's track (a playlist may have moved on by itself at the end of a track). */
const onTrack = (p: YtPlayer, m: TableMusic) => !m.playlistId || p.getPlaylistIndex() === m.index;

/**
 * The table's music on one screen (G): a small YouTube player in a corner of the board (visible, as YouTube requires),
 * kept on the GM's track and position. The browser plays sound only after a click: "Enable sound" unlocks it. Volume
 * and mute are each person's own. "Hide" stops the music on this screen only (a hidden player can't keep playing).
 */
export function MusicPlayer({ music, prefs, onPrefs, onBlocked, onEnded }: {
  music: TableMusic | null;
  prefs: TablePrefs;
  onPrefs: (patch: Partial<TablePrefs>) => void;
  /** The video can't play outside YouTube (errors 101/150): the GM is told. */
  onBlocked?: () => void;
  /** The track ended (the GM's screen moves the whole table to the next one of the playlist). */
  onEnded?: () => void;
}) {
  const host = useRef<HTMLDivElement>(null);
  const player = useRef<YtPlayer | null>(null);
  const loaded = useRef<string | null>(null);
  const live = useRef(music);
  const [ready, setReady] = useState(false);
  const [hidden, setHidden] = useState(false);
  // Browsers play sound only after a click on this page load: not kept across visits.
  const [unlocked, setUnlocked] = useState(false);
  // Callbacks change on every render of the page: the player reads the latest ones and is not rebuilt for them.
  const callbacks = useRef({ onBlocked, onEnded });
  useEffect(() => { live.current = music; callbacks.current = { onBlocked, onEnded }; });

  const show = music != null && !hidden;

  // The player is created once there is music, and destroyed with it.
  useEffect(() => {
    if (!show) return;
    let gone = false;
    void loadYouTube().then((YT) => {
      if (gone || !host.current) return;
      const el = document.createElement('div');
      host.current.appendChild(el);
      player.current = new YT.Player(el, {
        width: '100%', height: '100%',
        playerVars: { autoplay: 0, controls: 0, disablekb: 1, modestbranding: 1, playsinline: 1, rel: 0 },
        events: {
          onReady: () => { if (!gone) setReady(true); },
          onError: (e) => { if (isEmbedBlocked(e.data)) callbacks.current.onBlocked?.(); },
          // 0 = ended: the GM's screen moves everyone to the next track of the playlist.
          onStateChange: (e) => { if (e.data === 0) callbacks.current.onEnded?.(); },
        },
      });
    }, () => { /* YouTube unreachable: the table goes on without music */ });
    return () => {
      gone = true;
      player.current?.destroy();
      player.current = null;
      loaded.current = null;
      setReady(false);
    };
  }, [show]);

  // Follows the GM: another track loads; the same one seeks when it drifted, and plays or pauses.
  useEffect(() => {
    const p = player.current;
    if (!ready || !p || !music) return;
    const key = `${music.videoId ?? ''}|${music.playlistId ?? ''}|${music.index}`;
    const at = expectedPosition(music, Date.now());
    if (loaded.current !== key) {
      loaded.current = key;
      if (music.playlistId) p.loadPlaylist({ list: music.playlistId, listType: 'playlist', index: music.index, startSeconds: at });
      else if (music.videoId) p.loadVideoById(music.videoId, at);
    } else if (onTrack(p, music) && needsSeek(p.getCurrentTime(), at)) p.seekTo(at, true);
    if (music.playing && unlocked) p.playVideo(); else p.pauseVideo();
  }, [ready, music, unlocked]);

  // Volume and mute are this person's; the GM's volume scales them.
  useEffect(() => {
    const p = player.current;
    if (!ready || !p || !music) return;
    p.setVolume(Math.round((music.volume * prefs.musicVolume) / 100));
    if (prefs.musicMuted || !unlocked) p.mute(); else p.unMute();
  }, [ready, music, prefs.musicVolume, prefs.musicMuted, unlocked]);

  // Drift correction while playing (ads, a slow connection).
  useEffect(() => {
    if (!ready) return;
    const id = window.setInterval(() => {
      const p = player.current;
      const m = live.current;
      if (!p || !m?.playing) return;
      const at = expectedPosition(m, Date.now());
      // A playlist that moved on by itself waits for the table's next track instead of seeking the new one.
      if (onTrack(p, m) && needsSeek(p.getCurrentTime(), at)) p.seekTo(at, true);
    }, CHECK_MS);
    return () => window.clearInterval(id);
  }, [ready]);

  if (!music) return null;
  if (hidden) {
    return (
      <button type="button" className={styles.musicPill} onClick={() => setHidden(false)} title={t('Mostrar a música da mesa', 'Show the table music')}>
        <Music size={14} /> {music.title ?? t('Música da mesa', 'Table music')}
      </button>
    );
  }
  return (
    <div className={styles.musicBox} role="region" aria-label={t('Música da mesa', 'Table music')}>
      <div className={styles.musicBar}>
        <Music size={13} aria-hidden />
        <span className={styles.musicTitle}>{music.title ?? t('Música da mesa', 'Table music')}</span>
        <input type="range" min={0} max={100} value={prefs.musicVolume} aria-label={t('Seu volume', 'Your volume')}
          onChange={(e) => onPrefs({ musicVolume: Number(e.target.value) })} />
        <IconButton label={prefs.musicMuted ? t('Ligar som', 'Unmute') : t('Mudo', 'Mute')} onClick={() => onPrefs({ musicMuted: !prefs.musicMuted })}>
          {prefs.musicMuted ? <VolumeX size={13} /> : <Volume2 size={13} />}
        </IconButton>
        <IconButton label={t('Esconder (para a música só nesta tela)', 'Hide (stops the music on this screen only)')} onClick={() => setHidden(true)}><X size={13} /></IconButton>
      </div>
      <div ref={host} className={styles.musicFrame} />
      {!unlocked && (
        <div className={styles.musicUnlock}>
          <Button variant="primary" icon={<Volume2 size={14} />} onClick={() => setUnlocked(true)}>{t('Ativar som', 'Enable sound')}</Button>
        </div>
      )}
    </div>
  );
}
