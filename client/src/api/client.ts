import axios from 'axios'

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5290/api/v1',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true, // send httpOnly cookies on every request
})

// Prevent multiple concurrent refresh attempts
let isRefreshing = false
let refreshQueue: Array<() => void> = []

function processQueue() {
  refreshQueue.forEach((resolve) => resolve())
  refreshQueue = []
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    // Don't retry auth endpoints or requests already retried
    const isAuthEndpoint = originalRequest?.url?.includes('/auth/')
    if (error.response?.status !== 401 || isAuthEndpoint || originalRequest?._retry) {
      return Promise.reject(error)
    }

    if (isRefreshing) {
      // Queue the retry until the ongoing refresh completes
      return new Promise<void>((resolve) => {
        refreshQueue.push(resolve)
      }).then(() => apiClient(originalRequest))
    }

    originalRequest._retry = true
    isRefreshing = true

    try {
      await apiClient.post('/auth/refresh')
      processQueue()
      return apiClient(originalRequest)
    } catch {
      // Refresh failed — session is over
      window.dispatchEvent(new CustomEvent('auth:unauthorized'))
      return Promise.reject(error)
    } finally {
      isRefreshing = false
    }
  }
)

export default apiClient
