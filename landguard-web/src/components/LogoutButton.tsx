import { Button } from '@mui/material';
import LogoutIcon from '@mui/icons-material/Logout';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

interface LogoutButtonProps {
  fullWidth?: boolean;
}

/**
 * Shared logout control - one implementation reused by every dashboard
 * placeholder instead of each page wiring `useAuth().logout()` itself.
 * Explicitly navigates to /login after clearing auth state rather than
 * relying solely on ProtectedRoute noticing the state change on its next
 * render - both would eventually land the user on /login, but this makes
 * it immediate rather than dependent on re-render timing.
 */
export function LogoutButton({ fullWidth }: LogoutButtonProps) {
  const { logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <Button
      variant="outlined"
      color="inherit"
      startIcon={<LogoutIcon />}
      onClick={handleLogout}
      fullWidth={fullWidth}
    >
      Logout
    </Button>
  );
}
