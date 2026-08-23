import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { useAuth, homeFor } from './auth';
import { Button, FormField, Input } from '@/components/ui';
import { DisclaimerBanner } from '@/components/ui';
import s from './LoginPage.module.css';

const schema = z.object({ username: z.string().min(1), password: z.string().min(1) });
type Form = z.infer<typeof schema>;

export function LoginPage() {
  const { t } = useTranslation();
  const { login, ready, demoMode, user } = useAuth();
  const navigate = useNavigate();
  const [failed, setFailed] = useState(false);
  const { register, handleSubmit, formState } = useForm<Form>({ resolver: zodResolver(schema) });

  if (user) {
    navigate(homeFor(user.role), { replace: true });
    return null;
  }

  const onSubmit = handleSubmit(async (values) => {
    setFailed(false);
    try {
      await login(values.username, values.password);
      navigate('/', { replace: true });
    } catch {
      setFailed(true);
    }
  });

  return (
    <div className={s.wrap}>
      <form className={s.card} onSubmit={onSubmit} noValidate>
        <div className={s.brand}>
          <ShieldCheck size={28} color="var(--ok)" aria-hidden />
          <div>
            <h1>{t('app.name')}</h1>
            <p className="muted">{t('auth.subtitle')}</p>
          </div>
        </div>
        {!ready && demoMode && <p className="muted">{t('auth.autoLogin')}</p>}
        <FormField label={t('auth.username')} error={formState.errors.username && t('common.required')} required>
          {(id) => <Input id={id} autoComplete="username" invalid={!!formState.errors.username} {...register('username')} />}
        </FormField>
        <FormField label={t('auth.password')} error={formState.errors.password && t('common.required')} required>
          {(id) => <Input id={id} type="password" autoComplete="current-password" invalid={!!formState.errors.password} {...register('password')} />}
        </FormField>
        {failed && (
          <p role="alert" style={{ color: 'var(--crit)' }}>
            {t('auth.invalid')}
          </p>
        )}
        <Button type="submit" variant="primary" size="lg" loading={formState.isSubmitting}>
          {formState.isSubmitting ? t('auth.loggingIn') : t('auth.login')}
        </Button>
        <p className={s.hint}>{t('auth.demoHint')}</p>
      </form>
      <DisclaimerBanner />
    </div>
  );
}
