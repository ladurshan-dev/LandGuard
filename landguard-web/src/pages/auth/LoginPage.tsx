import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import type { Location } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Container,
  IconButton,
  InputAdornment,
  Paper,
  TextField,
  Typography,
} from '@mui/material';
import ShieldIcon from '@mui/icons-material/Shield';
import VisibilityIcon from '@mui/icons-material/Visibility';
import VisibilityOffIcon from '@mui/icons-material/VisibilityOff';
import { FullScreenLoader } from '../../components/FullScreenLoader';
import { useAuth } from '../../hooks/useAuth';
import { DASHBOARD_PATH_BY_ROLE } from '../../types/auth';
import type { LoginRequest } from '../../types/auth';

interface LoginFormValues {
  email: string;
  password: string;
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * The public login screen - the only place credentials are collected.
 * Contains no authentication logic of its own: field-level validation is
 * React Hook Form's job, the actual login call and session persistence
 * are AuthContext's (via useAuth().login), and translating a failure into
 * a safe message is authService's (this component just displays
 * `error.message`). That split is what "do not duplicate authentication
 * logic in LoginPage" means in practice here.
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
        alignItems: 'center',
        justifyContent: 'center',
        bgcolor: 'grey.100',
        px: 2,
      }}
    >
      <Container maxWidth="xs" disableGutters>
        <Paper elevation={3} sx={{ p: { xs: 3, sm: 4 }, borderRadius: 2 }}>
          <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', mb: 3 }}>
            <ShieldIcon color="primary" sx={{ fontSize: 40, mb: 1 }} />
            <Typography variant="h5" component="h1" align="center" sx={{ fontWeight: 600 }}>
              LandGuard
            </Typography>
            <Typography variant="body2" color="text.secondary" align="center">
              Land Deed Fraud Detection System
            </Typography>
          </Box>

          <Typography variant="subtitle1" component="h2" sx={{ mb: 2 }}>
            Sign in to your account
          </Typography>

          {submitError && (
            <Alert severity="error" role="alert" sx={{ mb: 2 }}>
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
              sx={{ mt: 3, py: 1.25 }}
              startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : undefined}
            >
              {isSubmitting ? 'Signing in...' : 'Sign in'}
            </Button>
          </Box>
        </Paper>
      </Container>
    </Box>
  );
}
