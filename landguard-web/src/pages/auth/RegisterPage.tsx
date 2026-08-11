import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Navigate, Link as RouterLink, useNavigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  InputAdornment,
  Link,
  MenuItem,
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
import type { RegisterRequest } from '../../types/auth';
import { landguardColors } from '../../theme/landguardTheme';

/**
 * The public self-registration screen (POST /api/auth/register), styled to
 * match LoginPage's split-panel design exactly - same brand panel, same
 * trust messaging, same Paper/TextField shape - so Register never looks
 * like a bolted-on afterthought. Only Buyer and Seller are offered as
 * roles; there is no way to reach an Admin account creation from here, and
 * the backend enforces that same restriction independently (see
 * RegisterRequestValidator on the backend) even if this form were bypassed.
 */

interface RegisterFormValues {
  fullName: string;
  email: string;
  role: 'Buyer' | 'Seller';
  nic: string;
  password: string;
  confirmPassword: string;
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
/** Mirrors AuthValidationRules.PasswordPattern on the backend exactly. */
const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;
/** Mirrors AuthValidationRules.NicPattern on the backend exactly - old format 9 digits + V/X, new format 12 digits. */
const NIC_PATTERN = /^([0-9]{9}[VvXx]|[0-9]{12})$/;

const TRUST_POINTS = [
  { Icon: LockIcon, label: 'Secure & encrypted access' },
  { Icon: VerifiedUserIcon, label: 'Verified listings only' },
  { Icon: GppGoodIcon, label: 'Fraud awareness protection' },
];

export default function RegisterPage() {
  const { register: registerAccount, isAuthenticated, isInitializing, user } = useAuth();
  const navigate = useNavigate();
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({
    defaultValues: { fullName: '', email: '', role: 'Buyer', nic: '', password: '', confirmPassword: '' },
    mode: 'onBlur',
  });

  const selectedRole = watch('role');
  const password = watch('password');

  // Same reasoning as LoginPage: don't flash the form for a visitor who
  // still has a valid session, or one who just isn't logged in yet.
  if (isInitializing) {
    return <FullScreenLoader />;
  }

  if (isAuthenticated && user) {
    return <Navigate to={DASHBOARD_PATH_BY_ROLE[user.role]} replace />;
  }

  const onSubmit = async (values: RegisterFormValues) => {
    setSubmitError(null);

    const request: RegisterRequest = {
      fullName: values.fullName.trim(),
      email: values.email.trim(),
      password: values.password,
      confirmPassword: values.confirmPassword,
      role: values.role,
      ...(values.role === 'Seller' ? { nic: values.nic.trim() } : {}),
    };

    try {
      const newUser = await registerAccount(request);
      navigate(DASHBOARD_PATH_BY_ROLE[newUser.role], { replace: true });
    } catch (error) {
      // authService.register already normalized this into a short, safe
      // message - e.g. "This email address is already registered.",
      // "This NIC is already linked to another account.", or a
      // FluentValidation message like "Passwords do not match." - never a
      // raw stack trace or backend payload.
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
      {/* BRAND PANEL - identical to LoginPage's. */}
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

      {/* FORM PANEL */}
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
            maxWidth: 460,
            p: { xs: 3, sm: 5 },
            border: '1px solid',
            borderColor: 'divider',
            borderRadius: 4,
          }}
        >
          <Box sx={{ textAlign: 'center', mb: 3.5 }}>
            <ShieldIcon color="primary" sx={{ fontSize: 38, mb: 1 }} />
            <Typography variant="h5" component="h2" sx={{ fontWeight: 400 }}>
              Create Account
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              Join LandGuard as a buyer or seller
            </Typography>
          </Box>

          {submitError && (
            <Alert severity="error" role="alert" sx={{ mb: 2.5 }}>
              {submitError}
            </Alert>
          )}

          <Box component="form" noValidate onSubmit={handleSubmit(onSubmit)}>
            <TextField
              {...register('fullName', {
                required: 'Full Name is required.',
                maxLength: { value: 150, message: 'Full Name must be 150 characters or fewer.' },
              })}
              label="Full Name"
              autoComplete="name"
              fullWidth
              margin="normal"
              disabled={isSubmitting}
              error={Boolean(errors.fullName)}
              helperText={errors.fullName?.message}
            />

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
            />

            <TextField
              {...register('role', { required: 'Role is required.' })}
              select
              label="I am a"
              fullWidth
              margin="normal"
              disabled={isSubmitting}
              error={Boolean(errors.role)}
              helperText={errors.role?.message}
            >
              <MenuItem value="Buyer">Buyer</MenuItem>
              <MenuItem value="Seller">Seller</MenuItem>
            </TextField>

            {/* NIC is required for Seller only - Buyer never sees or submits it, matching BuyerRegisterRequest's optional Nic on the backend. */}
            {selectedRole === 'Seller' && (
              <TextField
                {...register('nic', {
                  required: 'Seller NIC is required.',
                  pattern: { value: NIC_PATTERN, message: 'Invalid NIC format.' },
                })}
                label="NIC Number"
                fullWidth
                margin="normal"
                disabled={isSubmitting}
                error={Boolean(errors.nic)}
                helperText={errors.nic?.message ?? 'Sri Lankan NIC: 9 digits + V/X, or 12 digits.'}
              />
            )}

            <TextField
              {...register('password', {
                required: 'Password is required.',
                pattern: { value: PASSWORD_PATTERN, message: 'Password does not meet requirements.' },
              })}
              label="Password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="new-password"
              fullWidth
              margin="normal"
              disabled={isSubmitting}
              error={Boolean(errors.password)}
              helperText={
                errors.password?.message ??
                'At least 8 characters, with an uppercase letter, a lowercase letter, and a digit.'
              }
              slotProps={{
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

            <TextField
              {...register('confirmPassword', {
                required: 'Confirm Password is required.',
                validate: (value) => value === password || 'Passwords do not match.',
              })}
              label="Confirm Password"
              type={showConfirmPassword ? 'text' : 'password'}
              autoComplete="new-password"
              fullWidth
              margin="normal"
              disabled={isSubmitting}
              error={Boolean(errors.confirmPassword)}
              helperText={errors.confirmPassword?.message}
              slotProps={{
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton
                        aria-label={showConfirmPassword ? 'Hide password' : 'Show password'}
                        onClick={() => setShowConfirmPassword((prev) => !prev)}
                        edge="end"
                        tabIndex={-1}
                      >
                        {showConfirmPassword ? <VisibilityOffIcon /> : <VisibilityIcon />}
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
              {isSubmitting ? 'Creating account...' : 'Create account'}
            </Button>

            <Typography variant="body2" color="text.secondary" sx={{ mt: 2.5, textAlign: 'center' }}>
              Already have an account?{' '}
              <Link component={RouterLink} to="/login" underline="hover">
                Sign in
              </Link>
            </Typography>
          </Box>
        </Paper>
      </Box>
    </Box>
  );
}
