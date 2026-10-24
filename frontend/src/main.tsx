import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ApiError } from './api/client';
import { CompareProvider } from './state/CompareContext';
import { EffectsProvider } from './state/EffectsContext';
import { ToastProvider } from './state/ToastContext';
import App from './App';
import './styles/global.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      // Client errors (400/404) will not fix themselves by retrying.
      retry: (count, error) => !(error instanceof ApiError && error.status >= 400 && error.status < 500) && count < 2,
    },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <EffectsProvider>
          <ToastProvider>
            <CompareProvider>
              <App />
            </CompareProvider>
          </ToastProvider>
        </EffectsProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
