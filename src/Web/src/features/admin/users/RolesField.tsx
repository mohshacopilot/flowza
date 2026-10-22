import { Checkbox, Typography } from 'antd';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { SystemRole } from '../../../shared/auth/systemRoles';
import { roleLabel } from './roleLabel';
import { assignableRoles } from './userSchema';
import styles from './users.module.css';

interface RolesFieldProps {
  /** The chosen roles; User may be in it or not, it is always shown as chosen. */
  value: readonly string[];
  onChange: (roles: string[]) => void;
  disabled?: boolean;
}

/**
 * The system roles as check boxes in a fieldset (IAM-02-AC2: several roles per user). User is always chosen and
 * cannot be removed, because every active user has it.
 */
export function RolesField({ value, onChange, disabled = false }: RolesFieldProps) {
  const { t } = useTranslation();
  const helpId = useId();
  const chosen = [...new Set([...value, SystemRole.User])];
  const options = [
    ...assignableRoles.map((role) => ({ value: role, label: roleLabel(t, role), disabled })),
    { value: SystemRole.User, label: roleLabel(t, SystemRole.User), disabled: true },
  ];
  return (
    <fieldset className={styles.fieldset} aria-describedby={helpId}>
      <legend className={styles.legend}>{t('users.fields.roles')}</legend>
      <Checkbox.Group
        options={options}
        value={chosen}
        onChange={(roles) => {
          onChange(roles.filter((role) => role !== SystemRole.User));
        }}
      />
      <Typography.Paragraph type="secondary" id={helpId}>
        {t('users.fields.userRoleHelp')}
      </Typography.Paragraph>
    </fieldset>
  );
}
