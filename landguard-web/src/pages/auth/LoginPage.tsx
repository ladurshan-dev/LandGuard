import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import type { Location } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  InputAdornment,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import ShieldIcon from '@mui/icons-material/Shield';
import VerifiedUserIcon from '@mui/icons-material/VerifiedUser';
import LockIcon from '@mui/icons-material/Lock';
import GppGoodIcon from '@mui/icons-material/GppGood';
import VisibilityIcon from '@mui/icons-material/Visibility';
import VisibilityOffIcon from '@mui/icons-material/VisibilityOff';
import { FullScreenLoader } from '../../components/FullScreenLoader';
import { useAuth } from '../../hooks/useAuth';
import { DASHBOARD_PATH_BY_ROLE } from '../../types/auth';
import type { LoginRequest } from '../../types/auth';
import { landguardColors } from '../../theme/landguardTheme';

interface LoginFormValues {
  email: string;
  password: string;
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Trust messaging for the brand panel - copy only, no platform statistics or other data that would need to come from the backend. */
const TRUST_POINTS = [
  { Icon: LockIcon, label: 'Secure & encrypted access' },
  { Icon: VerifiedUserIcon, label: 'Verified listings only' },
  { Icon: GppGoodIcon, label: 'Fraud awareness protection' },
];

/**
 * The public login screen - Stage 2 visual redesign (see the attached
 * design reference approved for this file). Contains no authentication
 * logic of its own, unchanged from before this redesign: field-level
 * validation is React Hook Form's job, the actual login call and session
 * persistence are AuthContext's (via useAuth().login), and translating a
 * failure into a safe message is authService's (this component just
 * displays `error.message`). Every hook, validation rule, submit handler
 * and redirect guard below is identical to the pre-redesign version - only
 * the JSX around them changed.
 */
export default function LoginPage() {
  const { login, isAuthenticated, isInitializing, user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [showPassword, setShowPassword] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    defaultValues: { email: '', password: '' },
    mode: 'onBlur',
  });

  // Still reading localStorage - render nothing but a spinner rather than
  // risk a flash of the login form for a visitor who turns out to already
  // have a valid session.
  if (isInitializing) {
    return <FullScreenLoader />;
  }

  // Already logged in (session restored on refresh, or the user navigated
  // back to /login manually) - go straight to where they were headed, or
  // their dashboard, instead of showing the form again.
  if (isAuthenticated && user) {
    const state = location.state as { from?: Location } | null;
    const redirectTo = state?.from?.pathname ?? DASHBOARD_PATH_BY_ROLE[user.role];
    return <Navigate to={redirectTo} replace />;
  }

  const onSubmit = async (values: LoginFormValues) => {
    setSubmitError(null);

    const credentials: LoginRequest = {
      email: values.email.trim(),
      password: values.password,
    };

    try {
      const loggedInUser = await login(credentials);
      navigate(DASHBOARD_PATH_BY_ROLE[loggedInUser.role], { replace: true });
    } catch (error) {
      // authService already normalized this into a short, safe message
      // (invalid credentials / network failure / backend unavailable /
      // unrecognized role) - never a raw stack trace or backend payload.
      setSubmitError(error instanceof Error ? error.message : 'Something went wrong. Please try again.');
    }
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: { xs: 'column', md: 'row' },
        overflowX: 'hidden',
      }}
    >
      {/* BRAND PANEL - desktop: tall left column with trust messaging; mobile: compact top band. */}
      <Box
        sx={{
          position: 'relative',
          overflow: 'hidden',
          flex: { xs: '0 0 auto', md: '1 1 50%' },
          minHeight: { xs: 200, md: '100vh' },
          display: 'flex',
          flexDirection: 'column',
          justifyContent: { xs: 'center', md: 'flex-end' },
          px: { xs: 3, md: 7 },
          py: { xs: 4, md: 7 },
          color: '#fff',
          background: `linear-gradient(135deg, ${landguardColors.green} 0%, ${landguardColors.charcoal} 100%)`,
        }}
      >
        <Box
          sx={{
            position: 'absolute',
            inset: 0,
            opacity: 0.6,
            background: `radial-gradient(circle at 15% 20%, ${landguardColors.greenLight}55, transparent 45%), radial-gradient(circle at 85% 85%, ${landguardColors.gold}33, transparent 40%)`,
            pointerEvents: 'none',
          }}
        />

        <Box sx={{ position: 'relative' }}>
          <Box
            sx={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 1,
              bgcolor: 'rgba(200,146,42,0.20)',
              border: '1px solid rgba(200,146,42,0.4)',
              color: landguardColors.goldLight,
              px: 2,
              py: 0.75,
              borderRadius: 5,
              fontSize: 12,
              fontWeight: 600,
              mb: { xs: 2, md: 3 },
            }}
          >
            <ShieldIcon sx={{ fontSize: 16 }} />
            LandGuard &middot; Sri Lanka
          </Box>

          <Typography
            variant="h3"
            component="h1"
            sx={{ fontWeight: 400, lineHeight: 1.15, mb: { xs: 1, md: 2 }, fontSize: { xs: 26, md: 40 } }}
          >
            Safe Land Transactions
            <Box component="span" sx={{ display: 'block', color: landguardColors.goldLight, fontStyle: 'italic' }}>
              for Everyone.
            </Box>
          </Typography>

          <Typography
            sx={{
              display: { xs: 'none', md: 'block' },
              color: 'rgba(255,255,255,0.72)',
              maxWidth: 380,
              lineHeight: 1.7,
              mb: 4,
            }}
          >
            Helping buyers and sellers connect with trust and transparency across Sri Lanka.
          </Typography>

          <Stack spacing={1.25} sx={{ display: { xs: 'none', md: 'flex' } }}>
            {TRUST_POINTS.map(({ Icon, label }) => (
              <Box key={label} sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
                <Icon sx={{ fontSize: 18, color: landguardColors.goldLight }} />
                <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.82)' }}>
                  {label}
                </Typography>
              </Box>
            ))}
          </Stack>
        </Box>
      </Box>

      {/* FORM PANEL - unchanged authentication logic, redesigned presentation. */}
      <Box
        sx={{
          flex: { xs: '1 1 auto', md: '1 1 50%' },
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          bgcolor: 'background.default',
          px: { xs: 2.5, md: 4 },
          py: { xs: 4, md: 6 },
        }}
      >
        <Paper
          elevation={0}
          sx={{
            width: '100%',
            maxWidth: 440,
            p: { xs: 3, sm: 5 },
            border: '1px solid',
            borderColor: 'divider',
            borderRadius: 4,
          }}
        >
          <Box sx={{ textAlign: 'center', mb: 3.5 }}>
            <ShieldIcon color="primary" sx={{ fontSize: 38, mb: 1 }} />
            <Typography variant="h5" component="h2" sx={{ fontWeight: 400 }}>
              Welcome Back
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              Sign in to your account
            </Typography>
          </Box>

          {submitError && (
            <Alert severity="error" role="alert" sx={{ mb: 2.5 }}>
              {submitError}
            </Alert>
          )}

          <Box component="form" noValidate onSubmit={handleSubmit(onSubmit)}>
            <TextField
              {...register('email', {
                required: 'Email is required.',
                pattern: { value: EMAIL_PATTERN, message: 'Enter a valid email address.' },
              })}
              label="Email"
              type="email"
              autoComplete="email"
              fullWidth
              margin="normal"
              disabled={isSubmitting}
              error={Boolean(errors.email)}
              helperText={errors.email?.message}
              slotProps={{ htmlInput: { 'aria-label': 'Email address' } }}
            />

            <TextField
              {...register('password', { required: 'Password is required.' })}
              label="Password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              fullWidth
              margin="normal"
              disabled={isSubmitting}
              error={Boolean(errors.password)}
              helperText={errors.password?.message}
              slotProps={{
                htmlInput: { 'aria-label': 'Password' },
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton
                        aria-label={showPassword ? 'Hide password' : 'Show password'}
                        onClick={() => setShowPassword((prev) => !prev)}
                        edge="end"
                        tabIndex={-1}
                      >
                        {showPassword ? <VisibilityOffIcon /> : <VisibilityIcon />}
                      </IconButton>
                    </InputAdornment>
                  ),
                },
              }}
            />

            <Button
              type="submit"
              variant="contained"
              fullWidth
              size="large"
              disabled={isSubmitting}
              sx={{ mt: 3, py: 1.4 }}
              startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : undefined}
            >
              {isSubmitting ? 'Signing in...' : 'Sign in'}
            </Button>
          </Box>
        </Paper>
      </Box>
    </Box>
  );
}
