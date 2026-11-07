import { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { FastForward, Pause, Play, Rewind, SkipForward, Square } from 'lucide-react';
import { problemMessage } from '../../../api/characters';
import { tableApi } from '../../../api/table';
import type { TableMusicState } from '../../../api/tableTypes';
import { Button, IconButton } from '../../../components/ui/ui';
import { useToast } from '../../../state/ToastContext';
import { t } from '../../../i18n';
import { expectedPosition, musicKey } from './sync';
import styles from '../Table.module.css';

const clock = (s: number) => `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, '0')}`;

/**
 * The GM's music controls (G): paste a YouTube video or playlist link, play, pause, jump, next track, stop, and the
 * recent links. Every change goes to the players over the hub.
 */
export function MusicPanel() {
  const client = useQueryClient();
  const toast = useToast();
  const state = useQuery({ queryKey: musicKey, queryFn: tableApi.music });
  const [url, setUrl] = useState('');
  const [title, setTitle] = useState('');
  const music = state.data?.music ?? null;
  const apply = (p: Promise<TableMusicState>) => p.then((s) => client.setQueryData(musicKey, s), (e) => toast.notify(problemMessage(e), 'error'));
  // The clock ticks each second so the position shown follows the track.
  const [clockMs, setClockMs] = useState(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setClockMs(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, []);
  const now = music ? expectedPosition(music, clockMs) : 0;

  return (
    <div className={styles.stack}>
      <span className={styles.title}>{t('Música da mesa (YouTube)', 'Table music (YouTube)')}</span>
      <input aria-label={t('Link do YouTube', 'YouTube link')} placeholder={t('Cole o link de um vídeo ou playlist', 'Paste a video or playlist link')}
        value={url} onChange={(e) => setUrl(e.target.value)} />
      <div className={styles.row}>
        <input aria-label={t('Nome (opcional)', 'Name (optional)')} placeholder={t('Nome: combate, bar, tensão…', 'Name: fight, bar, tension…')}
          value={title} onChange={(e) => setTitle(e.target.value)} />
        <Button variant="primary" disabled={!url.trim()} onClick={() => apply(tableApi.loadMusic({ url, title: title || null })).then(() => { setUrl(''); setTitle(''); })}>
          {t('Carregar', 'Load')}
        </Button>
      </div>
      {music && (
        <div className={styles.stack}>
          <span>
            <strong>{music.title ?? music.videoId ?? music.playlistId}</strong>
            <span className={styles.muted}> · {music.playing ? t('tocando', 'playing') : t('pausada', 'paused')} · {clock(now)}</span>
          </span>
          <div className={styles.row}>
            {music.playing
              ? <Button icon={<Pause size={14} />} onClick={() => apply(tableApi.pauseMusic())}>{t('Pausar', 'Pause')}</Button>
              : <Button variant="primary" icon={<Play size={14} />} onClick={() => apply(tableApi.playMusic(null))}>{t('Tocar', 'Play')}</Button>}
            <IconButton label={t('Voltar 30 s', 'Back 30 s')} onClick={() => apply(tableApi.seekMusic(Math.max(0, now - 30)))}><Rewind size={14} /></IconButton>
            <IconButton label={t('Avançar 30 s', 'Forward 30 s')} onClick={() => apply(tableApi.seekMusic(now + 30))}><FastForward size={14} /></IconButton>
            {music.playlistId && <IconButton label={t('Próxima da playlist', 'Next in the playlist')} onClick={() => apply(tableApi.nextMusic())}><SkipForward size={14} /></IconButton>}
            <IconButton label={t('Parar para todos', 'Stop for everyone')} onClick={() => apply(tableApi.stopMusic())}><Square size={14} /></IconButton>
          </div>
          <label className={styles.row}>
            <span className={styles.label}>{t('Volume da mesa', 'Table volume')}</span>
            <input type="range" min={0} max={100} defaultValue={music.volume} key={music.volume}
              onPointerUp={(e) => apply(tableApi.musicVolume(Number((e.target as HTMLInputElement).value)))} />
          </label>
        </div>
      )}
      {(state.data?.recent.length ?? 0) > 0 && (
        <div className={styles.row}>
          <span className={styles.label}>{t('Recentes', 'Recent')}</span>
          {state.data!.recent.map((r) => (
            <button key={`${r.videoId}|${r.playlistId}`} type="button" className={styles.weaponChip}
              onClick={() => apply(tableApi.loadMusic({ url: r.playlistId && !r.videoId ? `https://www.youtube.com/playlist?list=${r.playlistId}` : `https://youtu.be/${r.videoId}${r.playlistId ? `?list=${r.playlistId}` : ''}`, title: r.title }))}>
              {r.title ?? r.videoId ?? r.playlistId}
            </button>
          ))}
        </div>
      )}
      <span className={styles.muted}>{t('Os jogadores ouvem no player do canto do mapa, cada um com seu volume. Alguns vídeos não deixam tocar fora do YouTube.', 'Players hear it in the player in the corner of the map, each with their own volume. Some videos can\'t play outside YouTube.')}</span>
    </div>
  );
}
