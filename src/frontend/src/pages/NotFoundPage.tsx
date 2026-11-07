import { Link } from 'react-router'

export function NotFoundPage() {
  return (
    <div className="mx-auto max-w-md py-24 text-center">
      <p className="text-sm font-medium text-violet-700 dark:text-violet-300">404</p>
      <h1 className="mt-2 text-2xl font-semibold">Bu sayfa bulunamadı</h1>
      <p className="mt-2 mb-8 text-sm text-gray-600 dark:text-gray-400">Bağlantı eski olabilir veya sayfa taşınmış olabilir. Özet panelinden devam edebilirsiniz.</p>
      <Link to="/" className="btn-primary">Özet paneline dön</Link>
    </div>
  )
}
