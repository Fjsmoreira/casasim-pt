import axios from 'axios'

const apiClient = axios.create({
  baseURL: '/api',
  timeout: 15_000,
  headers: { 'Content-Type': 'application/json' },
})

/** Attach the admin API key to outgoing requests if one is stored in sessionStorage. */
apiClient.interceptors.request.use((config) => {
  const key = sessionStorage.getItem('casasim-admin-key')
  if (key) {
    config.headers.set('X-Api-Key', key)
  }
  return config
})

export default apiClient
