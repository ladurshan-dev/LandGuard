import { useRef, useState } from 'react';
import { Alert, Box, Container, Paper, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import HeadphonesIcon from '@mui/icons-material/Headphones';
import { landguardColors } from '../../theme/landguardTheme';

/** Preferred shape from the brief - one centralized table, no per-language component duplication. */
interface PodcastLanguage {
  code: 'en' | 'si' | 'ta';
  label: string;
  nativeLabel: string;
  title: string;
  /** BCP-47 tag for the `lang` attribute on this language's title/audio player. */
  lang: string;
  src: string;
  mimeType: string;
  /** A plain, human-written estimate derived from the real file's duration (ffprobe: 176.26s / 182.23s / 198.45s) - not fetched at runtime, not fabricated. */
  durationLabel: string;
}

/**
 * Three real recordings supplied by the project owner (WhatsApp voice
 * notes), copied as-is into public/podcasts/ with clear names - no
 * transcoding, no TTS, no generated audio. Container/extension matches the
 * original source exactly (English/Sinhala stayed MP4+AAC, Tamil stayed
 * Ogg+Opus) - see HomePodcast's own investigation notes for why renaming
 * the extension alone without converting the container would have been
 * wrong. Language-to-file mapping was confirmed explicitly by the project
 * owner (not guessed from filename or duration) after this session found
 * no language metadata on any file and no transcription tool available in
 * this sandbox.
 */
const PODCAST_LANGUAGES: PodcastLanguage[] = [
  {
    code: 'en',
    label: 'English',
    nativeLabel: 'English',
    title: 'LandGuard — English Guide',
    lang: 'en',
    src: '/podcasts/landguard-english.mp4',
    mimeType: 'audio/mp4',
    durationLabel: 'Approx. 3 minutes (2 min 56 sec)',
  },
  {
    code: 'si',
    label: 'Sinhala',
    nativeLabel: 'සිංහල',
    title: 'LandGuard — සිංහල මාර්ගෝපදේශය',
    lang: 'si',
    src: '/podcasts/landguard-sinhala.mp4',
    mimeType: 'audio/mp4',
    durationLabel: 'Approx. 3 minutes (3 min 2 sec)',
  },
  {
    code: 'ta',
    label: 'Tamil',
    nativeLabel: 'தமிழ்',
    title: 'LandGuard — தமிழ் வழிகாட்டி',
    lang: 'ta',
    src: '/podcasts/landguard-tamil.ogg',
    mimeType: 'audio/ogg',
    durationLabel: 'Approx. 3 minutes (3 min 18 sec)',
  },
];

/**
 * Kept in English for all three languages on purpose - this session has no
 * verified Sinhala/Tamil translation of technical wording like "Government
 * Deed Verification" or "Supporting Risk Indicators" to draw on, and an
 * unverified machine translation of that specific legal/technical
 * distinction risks misrepresenting how the system actually works, which
 * matters more here than in decorative copy. Only the language button
 * labels and each card's title (both supplied/approved verbatim in the
 * brief) are shown in the native script. Real Sinhala/Tamil copy can
 * replace this the moment a verified translation is provided.
 */
const PODCAST_DESCRIPTION =
  "Learn how LandGuard handles property submission, deed comparison against government registry records, supporting risk indicators, administrator review, and approved listings.";

/**
 * "Learn About LandGuard" / multilingual audio guide - a single reusable
 * section, isolated from HomePage.tsx itself (which only renders
 * <HomePodcast />). One <audio> element is ever mounted at a time: it is
 * keyed on the selected language's code, so switching languages unmounts
 * the old element (the browser stops and discards that playback
 * completely) and mounts a brand-new one for the new source - no manual
 * document.getElementById, no risk of two recordings ever existing in the
 * DOM simultaneously. audioRef.current?.pause() is called immediately on
 * the click that changes languages too, purely to avoid a single stray
 * frame of overlapping sound while React processes the re-render; the key
 * remount is what actually guarantees the old element is gone.
 *
 * No autoplay anywhere (`controls` only, no `autoPlay`), preload="metadata"
 * on every language (so the homepage never downloads all three ~1-1.5MB
 * files on load - only their headers, until a visitor actually presses
 * play), and a per-language error state (`onError`) so one broken/missing
 * file never take down the other two or blank the whole section.
 */
export function HomePodcast() {
  const [selectedCode, setSelectedCode] = useState<PodcastLanguage['code']>('en');
  const [erroredCode, setErroredCode] = useState<PodcastLanguage['code'] | null>(null);
  const audioRef = useRef<HTMLAudioElement>(null);

  const selected = PODCAST_LANGUAGES.find((language) => language.code === selectedCode) ?? PODCAST_LANGUAGES[0];

  return (
    <Box
      component="section"
      aria-labelledby="landguard-podcast-heading"
      sx={{ bgcolor: 'background.paper', py: { xs: 6, md: 9 } }}
    >
      <Container maxWidth="md">
        <Typography
          variant="overline"
          sx={{ display: 'block', textAlign: 'center', color: 'secondary.main', fontWeight: 700, letterSpacing: 1 }}
        >
          LandGuard Audio Guide
        </Typography>
        <Typography
          id="landguard-podcast-heading"
          variant="h4"
          component="h2"
          sx={{ textAlign: 'center', fontWeight: 400, mb: 1 }}
        >
          Understand LandGuard in Your Language
        </Typography>
        <Typography sx={{ textAlign: 'center', color: 'text.secondary', maxWidth: 640, mx: 'auto', mb: 4 }}>
          Listen to a short introduction to how LandGuard supports safer and more transparent property listing and
          verification.
        </Typography>

        <Box sx={{ display: 'flex', justifyContent: 'center', mb: 4 }}>
          <ToggleButtonGroup
            value={selectedCode}
            exclusive
            onChange={(_event, newCode: PodcastLanguage['code'] | null) => {
              if (!newCode || newCode === selectedCode) {
                return;
              }
              // See this component's own doc comment - the key-remount
              // below is what actually stops the old recording; this just
              // removes any stray frame of overlap while React re-renders.
              audioRef.current?.pause();
              setSelectedCode(newCode);
              setErroredCode(null);
            }}
            aria-label="Choose audio guide language"
            color="primary"
            sx={{ flexWrap: 'wrap', justifyContent: 'center' }}
          >
            {PODCAST_LANGUAGES.map((language) => (
              <ToggleButton
                key={language.code}
                value={language.code}
                lang={language.lang}
                aria-label={`${language.label} audio guide`}
                sx={{ px: 3, textTransform: 'none', fontWeight: 600 }}
              >
                {language.nativeLabel}
              </ToggleButton>
            ))}
          </ToggleButtonGroup>
        </Box>

        <Paper variant="outlined" sx={{ p: { xs: 3, sm: 4 }, textAlign: 'center' }}>
          <Box
            sx={{
              width: 56,
              height: 56,
              borderRadius: '50%',
              bgcolor: landguardColors.greenPale,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              mx: 'auto',
              mb: 2,
            }}
          >
            <HeadphonesIcon sx={{ color: landguardColors.green }} />
          </Box>

          <Typography variant="h6" component="p" lang={selected.lang} sx={{ fontWeight: 600, mb: 0.5 }}>
            {selected.title}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            {selected.durationLabel}
          </Typography>

          {erroredCode === selected.code ? (
            <Alert severity="warning" sx={{ textAlign: 'left', maxWidth: 480, mx: 'auto' }}>
              Audio is temporarily unavailable.
            </Alert>
          ) : (
            <Box sx={{ display: 'flex', justifyContent: 'center' }}>
              {/*
                Plain native <audio>, not MUI's polymorphic Box - keeps the
                element's real HTMLAudioElement props/typing exact (ref,
                onError, preload) with nothing lost in translation through
                sx-only styling.
              */}
              <audio
                key={selected.code}
                ref={audioRef}
                controls
                preload="metadata"
                aria-label={`${selected.title} audio player`}
                onError={() => setErroredCode(selected.code)}
                style={{ width: '100%', maxWidth: 480 }}
              >
                <source src={selected.src} type={selected.mimeType} />
                Your browser does not support audio playback.
              </audio>
            </Box>
          )}

          <Typography variant="body2" color="text.secondary" sx={{ mt: 3, maxWidth: 520, mx: 'auto' }}>
            {PODCAST_DESCRIPTION}
          </Typography>

          <Typography variant="caption" color="text.disabled" sx={{ display: 'block', mt: 2 }}>
            Choose your preferred language and listen at your own pace.
          </Typography>
        </Paper>
      </Container>
    </Box>
  );
}
