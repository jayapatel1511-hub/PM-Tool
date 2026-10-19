import { trackRouteTiming } from '@/lib/route-counter'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { LucideProvider } from 'lucide-react'
import { StrictMode, Suspense } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider, matchRoutes } from 'react-router'
import '@/app/registrations'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'
import { Shell } from '@/app/Shell'
import { routes } from '@/app/routes'
import { shouldRetryRead } from '@/lib/api'
import { AuthGate } from '@/lib/auth'
import { Loading } from '@/components/hub/common'
import { RouterErrorPage, RootErrorBoundary } from '@/components/hub/error-page'
import { installClientErrorListeners, setClientErrorRoute } from '@/lib/client-errors'
import './index.css'


const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 15_000, retry: shouldRetryRead, refetchOnWindowFocus: true },
  },
})

const router = createBrowserRouter([{ path: '/', element: <Shell />, errorElement: <RouterErrorPage />, children: routes }])
setClientErrorRoute(() => matchRoutes(routes, window.location.pathname)?.map(m => m.route.path).filter(Boolean).join('/') ?? '/')
installClientErrorListeners()
trackRouteTiming(router)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <TooltipProvider delayDuration={300}>
        <LucideProvider strokeWidth={1.75}>
          <RootErrorBoundary><Suspense fallback={<Loading rows={4} />}>
            <AuthGate><RouterProvider router={router} /></AuthGate>
          </Suspense></RootErrorBoundary>
        </LucideProvider>
        <Toaster position="bottom-right" closeButton />
      </TooltipProvider>
    </QueryClientProvider>
  </StrictMode>,
)
